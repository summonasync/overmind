using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PointerDoorTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public PointerDoorTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));
        _materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public async Task InitializeAsync() =>
        await TestPlanningDispatcher.EnableSyncModeAsync(_context);

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task UpsertArtifact_PointerBody_StoresLineVerbatim()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();
        const string body = "<approach>\npointer: docs/plans/x.md#implementation\n</approach>";

        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Plan, body, null, null, null, default);

        artifact.Body.ShouldBe(body);
    }

    [Fact]
    public async Task UpsertArtifact_MalformedPointerLine_ThrowsTyped400WithoutPersisting()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertAsync(
                feature.Id, PlanningArtifactKind.Spec, "pointer: ../escape.md", null, null, null, default));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.InvalidPointerMarker);
        _context.PlanningArtifacts.Any(a => a.FeatureId == feature.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertTasks_MalformedPointerInItem_ThrowsWithBatchIndex()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.UpsertTasksAsync(feature.Id,
            [
                new PlanningTaskUpsert("# Task\n\n<goal>\nfine\n</goal>"),
                new PlanningTaskUpsert("# Task\n\n<goal>\npointer: docs\\x.md\n</goal>")
            ], default));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidPointerMarker);
        ex.Message.ShouldStartWith("tasks[1]:");
    }

    [Fact]
    public async Task PatchSection_MalformedPointerContent_ThrowsAndLeavesBodyUntouched()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "<scope>\nSpec body.\n</scope>", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(artifact.Id,
                new PlanningArtifactPatch(Section: "scope", Content: "rewritten\npointer: /abs.md"), default));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidPointerMarker);
        artifact.Body.ShouldBe("<scope>\nSpec body.\n</scope>");
    }

    [Fact]
    public async Task PatchSection_InlineFormMalformedMarker_CanonicalFormIsRejected()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "<scope>\nSpec body.\n</scope>", null, null, null, default);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => artifactService.PatchAsync(artifact.Id,
                new PlanningArtifactPatch(Section: "scope", Content: "<scope>pointer: ../escape.md</scope>"), default));

        ex.Code.ShouldBe(PlanningFailureCode.InvalidPointerMarker);
        artifact.Body.ShouldBe("<scope>\nSpec body.\n</scope>");
    }

    [Fact]
    public async Task PatchSection_InlineFormValidMarker_StoresCanonicalForm()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, "<scope>\nSpec body.\n</scope>", null, null, null, default);

        await artifactService.PatchAsync(artifact.Id,
            new PlanningArtifactPatch(
                Section: "scope", Content: "<scope>pointer: docs/plans/x.md#implementation</scope>"), default);

        artifact.Body.ShouldBe("<scope>\npointer: docs/plans/x.md#implementation\n</scope>");
    }

    [Fact]
    public async Task PatchSection_OtherSection_PreservesExistingPointerLine()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();
        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Plan,
            "<goal>\nPoint at the repo.\n</goal>\n\n<approach>\npointer: docs/plans/x.md#implementation\n</approach>",
            null, null, null, default);

        await artifactService.PatchAsync(
            artifact.Id, new PlanningArtifactPatch(Section: "goal", Content: "rewritten goal"), default);

        artifact.Body.ShouldContain("pointer: docs/plans/x.md#implementation");
        artifact.Body.ShouldContain("<goal>\nrewritten goal\n</goal>");
    }

    [Fact]
    public async Task CreateFeature_MalformedPointerBody_IsNeverValidated()
    {
        var featureService = CreateFeatureService();

        var feature = await featureService.CreateAsync(
            _projectId, "pointer-feature", "Title", "<goal>\npointer: ../bad.md\n</goal>");

        feature.Body.ShouldContain("pointer: ../bad.md");
    }

    [Fact]
    public async Task UpsertArtifact_LookalikeText_Untouched()
    {
        var artifactService = CreateArtifactService();
        var feature = await CreateFeatureAsync();
        const string body = "The pointer: convention needs whitespace and Pointer: is not a marker.";

        var artifact = await artifactService.UpsertAsync(
            feature.Id, PlanningArtifactKind.Spec, body, null, null, null, default);

        artifact.Body.ShouldBe(body);
    }

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private PlanningArtifactService CreateArtifactService() =>
        new(
            _context,
            TestPlanningDispatcher.Create(_context, _materializer),
            new TaskDependencyService(
                _context, TestPlanningDispatcher.Create(_context, _materializer)));

    private async Task<PlanningFeature> CreateFeatureAsync() =>
        await CreateFeatureService().CreateAsync(_projectId, "pointer-door", "Pointer door", "<goal>\nBody.\n</goal>");
}
