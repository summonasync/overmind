using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningExportServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public PlanningExportServiceTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-export-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        _dataDir = Path.Combine(_sandboxDir, "data");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Export_RendersTheSameBytesTheMaterializerWrites()
    {
        var projectId = await SeedProjectAsync();
        await SeedDefaultFeatureAsync(projectId);
        await SeedEntryAsync(projectId);
        var target = Path.Combine(_sandboxDir, "snapshot");

        await CreateService().ExportAsync(projectId, target, CancellationToken.None);

        await CreateMaterializer().SyncAsync(projectId, ct: CancellationToken.None);

        var exported = RelativeFiles(target);
        exported.ShouldNotBeEmpty();
        exported.ShouldBe(RelativeFiles(_projectDir));

        foreach (var relative in exported)
        {
            (await File.ReadAllBytesAsync(Path.Combine(target, relative)))
                .ShouldBe(await File.ReadAllBytesAsync(Path.Combine(_projectDir, relative)));
        }
    }

    [Fact]
    public async Task Export_LeavesNoTrackingRowsBehind()
    {
        var projectId = await SeedProjectAsync();
        await SeedDefaultFeatureAsync(projectId);
        var target = Path.Combine(_sandboxDir, "snapshot");

        await CreateService().ExportAsync(projectId, target, CancellationToken.None);

        using var ctx = _db.CreateContext();
        ctx.PlanningMaterializedFiles.Count(m => m.ProjectId == projectId).ShouldBe(0);
    }

    [Fact]
    public async Task Export_NonEmptyTarget_RefusesWithTypedConflict()
    {
        var projectId = await SeedProjectAsync();
        var target = Path.Combine(_sandboxDir, "snapshot");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "keep.md"), "user file");

        var failure = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ExportAsync(projectId, target, CancellationToken.None));

        failure.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        failure.Code.ShouldBe(PlanningFailureCode.ExportTargetNotEmpty);
        failure.Message.ShouldContain(target);
        File.Exists(Path.Combine(target, "keep.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_FileAsTarget_RefusesWithTypedConflict()
    {
        var projectId = await SeedProjectAsync();
        var target = Path.Combine(_sandboxDir, "snapshot.md");
        await File.WriteAllTextAsync(target, "user file");

        var failure = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ExportAsync(projectId, target, CancellationToken.None));

        failure.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        failure.Code.ShouldBe(PlanningFailureCode.ExportTargetNotEmpty);
        failure.Message.ShouldContain(target);
        File.ReadAllText(target).ShouldBe("user file");
    }

    [Fact]
    public async Task Export_ProjectRootAsTarget_RefusesWithTypedConflict()
    {
        var projectId = await SeedProjectAsync();

        var failure = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ExportAsync(projectId, _projectDir, CancellationToken.None));

        failure.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        failure.Code.ShouldBe(PlanningFailureCode.ExportTargetInsideMirror);
        Directory.EnumerateFileSystemEntries(_projectDir).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("BACKLOG")]
    [InlineData("ENTRIES")]
    public async Task Export_TargetInsideTheMirror_RefusesWithTypedConflict(string mirrorDir)
    {
        var projectId = await SeedProjectAsync();
        var target = Path.Combine(_projectDir, mirrorDir, "snapshot");

        var failure = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ExportAsync(projectId, target, CancellationToken.None));

        failure.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        failure.Code.ShouldBe(PlanningFailureCode.ExportTargetInsideMirror);
    }

    [Fact]
    public async Task Export_RelativeTarget_RefusesAsInvalidInput()
    {
        var projectId = await SeedProjectAsync();

        var failure = await Should.ThrowAsync<PlanningException>(
            () => CreateService().ExportAsync(projectId, "snapshot", CancellationToken.None));

        failure.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        failure.Code.ShouldBe(PlanningFailureCode.ExportTargetNotAbsolute);
    }

    [Fact]
    public async Task Export_PinnedRowEscapingTheTarget_RefusesThroughPathGuard()
    {
        var projectId = await SeedProjectAsync();
        Guid featureId;
        using (var seed = _db.CreateContext())
        {
            var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
            seed.PlanningFeatures.Add(feature);
            seed.PlanningArtifacts.Add(PlanningArtifact.Create(
                feature.Id, PlanningArtifactKind.Task, "no heading here", 2));
            await seed.SaveChangesAsync();
            featureId = feature.Id;
        }

        using (var seed = _db.CreateContext())
        {
            seed.PlanningMaterializedFiles.Add(PlanningMaterializedFile.Create(
                projectId, featureId, "BACKLOG/006-demo/tasks/02-../../escape.md", ContentHash.Sha256("x")));
            await seed.SaveChangesAsync();
        }

        var target = Path.Combine(_sandboxDir, "snapshot");
        var failure = await Should.ThrowAsync<MaterializationException>(
            () => CreateService().ExportAsync(projectId, target, CancellationToken.None));

        failure.Kind.ShouldBe(MaterializationFailureKind.PathTraversal);
        Directory.Exists(target).ShouldBeFalse();
    }

    private PlanningExportService CreateService() => new(_db.CreateContext());

    private PlanningMaterializer CreateMaterializer()
    {
        var ctx = _db.CreateContext();
        return new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);
    }

    private static string[] RelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

    private async Task<Guid> SeedProjectAsync()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedDefaultFeatureAsync(Guid projectId)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        feature.SetStatus(PlanningFeatureStatus.Ready);
        ctx.PlanningFeatures.Add(feature);

        var spec = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Spec, "# Spec Heading\n\n<scope>\nSpec body.\n</scope>");
        var taskOne = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "# First Task\n\n<goal>\nDo it.\n</goal>", 1);
        ctx.PlanningArtifacts.AddRange(spec, taskOne);
        await ctx.SaveChangesAsync();
    }

    private async Task SeedEntryAsync(Guid projectId)
    {
        using var ctx = _db.CreateContext();
        ctx.ContextEntries.Add(
            ContextEntry.Create(projectId, EntryType.Decision, "Use SQLite", "SQLite is the only metadata store."));
        await ctx.SaveChangesAsync();
    }
}
