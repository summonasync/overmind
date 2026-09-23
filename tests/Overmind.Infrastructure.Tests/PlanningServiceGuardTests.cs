using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningServiceGuardTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;

    public PlanningServiceGuardTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-plan-guard-{Guid.NewGuid():N}");
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
    public async Task Sync_Throws_DelegatesFailureRecordingInsteadOfPropagating()
    {
        var projectId = await SeedProjectAsync();
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("disk exploded"));

        await PlanningServiceGuard.SyncMaterializationAsync(
            materializer, NullLogger.Instance, projectId, CancellationToken.None);

        await materializer.Received(1).RecordSyncFailureAsync(
            projectId, Arg.Is<string>(m => m.Contains("disk exploded")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_Cancelled_DoesNotRecordFailure()
    {
        var projectId = await SeedProjectAsync();
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await PlanningServiceGuard.SyncMaterializationAsync(
            materializer, NullLogger.Instance, projectId, CancellationToken.None);

        await materializer.DidNotReceiveWithAnyArgs().RecordSyncFailureAsync(default, default!, default);
    }

    [Fact]
    public async Task Clear_Throws_DelegatesFailureRecordingInsteadOfPropagating()
    {
        var projectId = await SeedProjectAsync();
        var materializer = Substitute.For<IPlanningMaterializer>();
        materializer.ClearAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("target readonly"));

        await PlanningServiceGuard.ClearMaterializationAsync(
            materializer, NullLogger.Instance, projectId, Guid.NewGuid(), CancellationToken.None);

        await materializer.Received(1).RecordSyncFailureAsync(
            projectId, Arg.Is<string>(m => m.Contains("target readonly")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_RealFailureThroughGuard_PersistsErrorStateUntilNextSuccess()
    {
        var projectId = await SeedProjectAsync();
        await SeedFeatureWithTaskAsync(projectId);

        DateTime firstStamp;
        using (var ctx = _db.CreateContext())
        {
            await PlanningServiceGuard.SyncMaterializationAsync(
                CreateMaterializer(ctx), NullLogger.Instance, projectId, CancellationToken.None);
            firstStamp = (await ctx.Projects.SingleAsync(p => p.Id == projectId)).LastMaterializedAt!.Value;
        }

        var taskPath = Path.Combine(_projectDir, "BACKLOG", "006-demo", "tasks", "01-first-task.md");
        var pristine = await File.ReadAllTextAsync(taskPath);
        await File.AppendAllTextAsync(taskPath, "\nhand edit\n");

        using (var ctx = _db.CreateContext())
        {
            await PlanningServiceGuard.SyncMaterializationAsync(
                CreateMaterializer(ctx), NullLogger.Instance, projectId, CancellationToken.None);
        }

        using (var verify = _db.CreateContext())
        {
            var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
            project.LastMaterializationError.ShouldNotBeNull();
            project.LastMaterializationError!.ShouldContain("locally modified tracked files");
            project.LastMaterializationFailedAt.ShouldNotBeNull();
            project.LastMaterializedAt.ShouldBe(firstStamp);
        }

        await File.WriteAllTextAsync(taskPath, pristine);

        using (var ctx = _db.CreateContext())
        {
            await PlanningServiceGuard.SyncMaterializationAsync(
                CreateMaterializer(ctx), NullLogger.Instance, projectId, CancellationToken.None);
        }

        using (var verify = _db.CreateContext())
        {
            var project = await verify.Projects.SingleAsync(p => p.Id == projectId);
            project.LastMaterializationError.ShouldBeNull();
            project.LastMaterializationFailedAt.ShouldBeNull();
            project.LastMaterializedAt!.Value.ShouldBeGreaterThan(firstStamp);
        }
    }

    private async Task<Guid> SeedProjectAsync()
    {
        using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedFeatureWithTaskAsync(Guid projectId)
    {
        using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        ctx.PlanningFeatures.Add(feature);
        ctx.PlanningArtifacts.Add(PlanningArtifact.Create(
            feature.Id, PlanningArtifactKind.Task, "# First Task\n\n<goal>\nDo it.\n</goal>", 1));
        await ctx.SaveChangesAsync();
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);
}
