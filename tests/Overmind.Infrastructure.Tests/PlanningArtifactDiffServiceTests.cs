using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningArtifactDiffServiceTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public PlanningArtifactDiffServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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
    public async Task DiffAsync_MissingArtifact_ThrowsNotFound()
    {
        var service = CreateService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DiffAsync(Guid.NewGuid(), null, null));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.MissingArtifact);
    }

    [Fact]
    public async Task DiffAsync_DefaultPair_LatestVsPrevious()
    {
        var artifact = await CreateSpecAsync("v1 body", "v2 body", "v3 body");
        var service = CreateService();

        var result = await service.DiffAsync(artifact.Id, null, null);

        result.ArtifactId.ShouldBe(artifact.Id);
        result.FromVersion.ShouldBe(2);
        result.ToVersion.ShouldBe(3);
        result.Diff.ShouldContain("-v2 body");
        result.Diff.ShouldContain("+v3 body");
        result.Diff.ShouldNotContain("-v1 body");
        result.Diff.ShouldNotContain("+v1 body");
    }

    [Fact]
    public async Task DiffAsync_ExplicitFromTo_Honored()
    {
        var artifact = await CreateSpecAsync("v1 body", "v2 body", "v3 body");
        var service = CreateService();

        var result = await service.DiffAsync(artifact.Id, 1, 3);

        result.FromVersion.ShouldBe(1);
        result.ToVersion.ShouldBe(3);
        result.Diff.ShouldContain("-v1 body");
        result.Diff.ShouldContain("+v3 body");
        result.Diff.ShouldNotContain("v2 body");
    }

    [Fact]
    public async Task DiffAsync_FromEqualsTo_ThrowsInvalidInput()
    {
        var artifact = await CreateSpecAsync("v1 body", "v2 body", "v3 body");
        var service = CreateService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DiffAsync(artifact.Id, 2, 2));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.InvalidVersionRange);
    }

    [Fact]
    public async Task DiffAsync_FromGreaterThanTo_ThrowsInvalidInput()
    {
        var artifact = await CreateSpecAsync("v1 body", "v2 body", "v3 body");
        var service = CreateService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DiffAsync(artifact.Id, 3, 1));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.InvalidVersionRange);
    }

    [Fact]
    public async Task DiffAsync_UnknownVersion_ThrowsNotFound()
    {
        var artifact = await CreateSpecAsync("v1 body", "v2 body", "v3 body");
        var service = CreateService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DiffAsync(artifact.Id, 1, 99));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.ArtifactVersionNotFound);
    }

    [Fact]
    public async Task DiffAsync_FirstVersionWithoutPrevious_DefaultPair_ThrowsNotFound()
    {
        var artifact = await CreateSpecAsync("only body");
        artifact.Version.ShouldBe(1);
        var service = CreateService();

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.DiffAsync(artifact.Id, null, null));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
        ex.Code.ShouldBe(PlanningFailureCode.ArtifactVersionNotFound);
    }

    private PlanningArtifactDiffService CreateService() => new(_context);

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

    private async Task<PlanningArtifact> CreateSpecAsync(params string[] bodies)
    {
        var feature = await CreateFeatureService()
            .CreateAsync(_projectId, $"diff-{Guid.NewGuid():N}", "Diff fixture", "body");
        var artifacts = CreateArtifactService();
        PlanningArtifact artifact = null!;

        foreach (var body in bodies)
        {
            artifact = await artifacts.UpsertAsync(
                feature.Id, PlanningArtifactKind.Spec, body, null, null, null);
        }

        return artifact!;
    }
}
