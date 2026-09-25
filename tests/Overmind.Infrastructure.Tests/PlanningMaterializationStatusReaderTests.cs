using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializationStatusReaderTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public PlanningMaterializationStatusReaderTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-plan-status-{Guid.NewGuid():N}");
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
    public async Task Read_AfterCleanPass_ReportsSuccessRunWithoutStalePaths()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var status = await CreateReader(ctx).ReadAsync(projectId, CancellationToken.None);

        status.ProjectId.ShouldBe(projectId);
        status.LastSuccess.ShouldNotBeNull();
        status.LastAttempt.ShouldNotBeNull();
        status.LastAttemptReason.ShouldBeNull();
        status.Strategy.ShouldBe(PlanningMaterialization.On);
        status.TargetDir.ShouldBe(Path.Combine(_projectDir, "BACKLOG"));
        status.TrackedFiles.ShouldBe(5);
        status.StalePaths.ShouldBeEmpty();

        var run = status.LastRun.ShouldNotBeNull();
        run!.Atomic.ShouldBeTrue();
        run.Written.Length.ShouldBe(5);
        run.Skipped.ShouldBeEmpty();
        run.Refused.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_DriftedTrackedFile_CountsItStaleDespiteBeingWritten()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);
        await File.AppendAllTextAsync(
            Path.Combine(_projectDir, "BACKLOG", "006-demo", "feature.md"), "\nhand edit\n");

        var status = await CreateReader(ctx).ReadAsync(projectId, CancellationToken.None);

        status.StalePaths.ShouldHaveSingleItem().ShouldBe("BACKLOG/006-demo/feature.md");
    }

    [Fact]
    public async Task Read_PlanPhaseFailureAfterSuccess_RendersNoReportForTheAttempt()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        var successFinishedAt = MaterializationRunJson.Deserialize(
            (await ctx.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializationRun)!.FinishedAt;

        await SeedFeatureAsync(projectId, "../evil", "Evil", "body", PlanningFeatureStatus.Draft);
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));
        await materializer.RecordSyncFailureAsync(projectId, ex.Message, CancellationToken.None);

        var project = await ctx.Projects.SingleAsync(p => p.Id == projectId);
        project.LastMaterializationAttemptAt.ShouldNotBeNull();
        project.LastMaterializationAttemptAt!.Value.ShouldBeGreaterThan(successFinishedAt);

        var status = await CreateReader(ctx).ReadAsync(projectId, CancellationToken.None);

        status.LastAttemptReason.ShouldNotBeNull();
        status.LastRun.ShouldBeNull();
        status.StalePaths.Count.ShouldBe(5);
        status.StalePaths.ShouldContain("BACKLOG/006-demo/feature.md");
    }

    [Fact]
    public async Task Read_KilledAttemptAfterSuccess_DropsThePreviousReportWithoutAReason()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);
        var successFinishedAt = MaterializationRunJson.Deserialize(
            (await ctx.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializationRun)!.FinishedAt;

        var project = await ctx.Projects.SingleAsync(p => p.Id == projectId);
        project.MarkMaterializationAttempt(successFinishedAt.AddMinutes(1));
        await ctx.SaveChangesAsync();

        var status = await CreateReader(ctx).ReadAsync(projectId, CancellationToken.None);

        status.LastAttempt.ShouldBe(successFinishedAt.AddMinutes(1));
        status.LastAttemptReason.ShouldBeNull();
        status.LastRun.ShouldBeNull();
        status.LastSuccess.ShouldNotBeNull();
    }

    [Fact]
    public async Task Read_RefusedRun_KeepsTheAbortedReportAndMarksTheRefusalStale()
    {
        var projectId = await SeedProjectAsync(PlanningMaterialization.On);
        await SeedDefaultFeatureAsync(projectId);

        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);
        await File.AppendAllTextAsync(
            Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md"), "\nhand edit\n");

        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));

        ex.Kind.ShouldBe(MaterializationFailureKind.Refused);

        var status = await CreateReader(ctx).ReadAsync(projectId, CancellationToken.None);

        status.LastAttemptReason.ShouldNotBeNull();
        status.LastAttemptReason!.ShouldContain("local edits");
        var run = status.LastRun.ShouldNotBeNull();
        run!.Atomic.ShouldBeFalse();
        run.Written.ShouldBeEmpty();
        run.Refused.ShouldNotBeEmpty();
        status.StalePaths.ShouldHaveSingleItem()
            .ShouldBe("BACKLOG/006-demo/tasks/01-first-task.md");
    }

    private async Task<Guid> SeedProjectAsync(PlanningMaterialization strategy)
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(strategy, HumanValidationOverride.Unset);
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
        var taskTwo = PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "no heading here", 2);

        ctx.PlanningArtifacts.AddRange(spec, taskOne, taskTwo);
        await ctx.SaveChangesAsync();
    }

    private async Task SeedFeatureAsync(
        Guid projectId, string slug, string title, string body, PlanningFeatureStatus status)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, slug, title, body);
        feature.SetStatus(status);
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    private PlanningMaterializationStatusReader CreateReader(OvermindDbContext ctx) =>
        new(
            ctx,
            new PlanningPolicy(ctx, new AppSettingsService(ctx)),
            CreateMaterializer(ctx),
            _dataDir);
}
