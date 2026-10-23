using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningMaterializationLagReaderTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly string _sandboxDir;
    private readonly string _projectDir;
    private readonly string _dataDir;
    private readonly PlanningMaterializationQueue _queue = new();

    public PlanningMaterializationLagReaderTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-plan-lag-{Guid.NewGuid():N}");
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
    public async Task Read_FreshProject_ReportsNothingPendingAndNothingStale()
    {
        var projectId = await SeedProjectAsync();
        using var ctx = _db.CreateContext();

        var lag = await CreateReader(ctx).ReadAsync(projectId, CancellationToken.None);

        lag.PendingMutations.ShouldBe(0);
        lag.LastAttemptAt.ShouldBeNull();
        lag.LastSuccessAt.ShouldBeNull();
        lag.Stale.ShouldBeFalse();
    }

    [Fact]
    public async Task Read_UnknownProject_ReportsZeroedLag()
    {
        using var ctx = _db.CreateContext();

        var lag = await CreateReader(ctx).ReadAsync(Guid.NewGuid(), CancellationToken.None);

        lag.PendingMutations.ShouldBe(0);
        lag.LastAttemptAt.ShouldBeNull();
        lag.LastSuccessAt.ShouldBeNull();
        lag.Stale.ShouldBeFalse();
    }

    [Fact]
    public async Task Read_EnqueuedMutation_ReportsPendingAndStaleUntilThePassLands()
    {
        var projectId = await SeedProjectAsync();
        using var ctx = _db.CreateContext();
        var reader = CreateReader(ctx);

        _queue.TryEnqueue(projectId).ShouldBeTrue();
        var enqueued = await reader.ReadAsync(projectId, CancellationToken.None);
        enqueued.PendingMutations.ShouldBe(1);
        enqueued.Stale.ShouldBeTrue();

        _queue.TryDequeue(out _).ShouldBeTrue();
        await CreateMaterializer(ctx).SyncAsync(projectId, ct: CancellationToken.None);

        var landed = await reader.ReadAsync(projectId, CancellationToken.None);
        landed.PendingMutations.ShouldBe(0);
        landed.Stale.ShouldBeFalse();
        landed.LastAttemptAt.ShouldNotBeNull();
        landed.LastSuccessAt.ShouldNotBeNull();
        landed.LastSuccessAt!.Value.ShouldBeGreaterThanOrEqualTo(landed.LastAttemptAt!.Value);
    }

    [Fact]
    public async Task Read_FailedAttemptAfterSuccess_StaysStaleUntilTheNextSuccess()
    {
        var projectId = await SeedProjectAsync();
        using var ctx = _db.CreateContext();
        var materializer = CreateMaterializer(ctx);
        var reader = CreateReader(ctx);
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        await SeedFeatureAsync(projectId, "../evil", "Evil", "body");
        var ex = await Should.ThrowAsync<MaterializationException>(
            () => materializer.SyncAsync(projectId, ct: CancellationToken.None));
        await materializer.RecordSyncFailureAsync(projectId, ex.Message, CancellationToken.None);

        var failed = await reader.ReadAsync(projectId, CancellationToken.None);
        failed.PendingMutations.ShouldBe(0);
        failed.Stale.ShouldBeTrue();

        await RemoveFeatureAsync(projectId, "../evil");
        await materializer.SyncAsync(projectId, ct: CancellationToken.None);

        var recovered = await reader.ReadAsync(projectId, CancellationToken.None);
        recovered.PendingMutations.ShouldBe(0);
        recovered.Stale.ShouldBeFalse();
    }

    private async Task<Guid> SeedProjectAsync()
    {
        await using var ctx = _db.CreateContext();
        var project = Project.Create("demo", _projectDir);
        project.SetPlanningWorkflow(PlanningMaterialization.On, HumanValidationOverride.Unset);
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        await SeedFeatureAsync(project.Id, "006-demo", "Demo Feature", "<goal>\nShip it.\n</goal>");
        return project.Id;
    }

    private async Task SeedFeatureAsync(Guid projectId, string slug, string title, string body)
    {
        await using var ctx = _db.CreateContext();
        var feature = PlanningFeature.Create(projectId, slug, title, body);
        ctx.PlanningFeatures.Add(feature);
        await ctx.SaveChangesAsync();
    }

    private async Task RemoveFeatureAsync(Guid projectId, string slug)
    {
        await using var ctx = _db.CreateContext();
        var feature = await ctx.PlanningFeatures
            .SingleAsync(f => f.ProjectId == projectId && f.Slug == slug);
        ctx.PlanningFeatures.Remove(feature);
        await ctx.SaveChangesAsync();
    }

    private PlanningMaterializer CreateMaterializer(OvermindDbContext ctx) =>
        new(ctx, new PlanningPolicy(ctx, new AppSettingsService(ctx)), _dataDir);

    private PlanningMaterializationLagReader CreateReader(OvermindDbContext ctx) =>
        new(ctx, _queue);
}
