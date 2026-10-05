using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PlanningStatusBackfillTests : IDisposable, IAsyncLifetime
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly AppSettingsService _settings;
    private readonly Guid _projectId;

    public PlanningStatusBackfillTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));
        _settings = new AppSettingsService(_context);

        var project = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    [Fact]
    public async Task RunAsync_InProgressWithoutTasks_MovesToReadyWithAutoLine()
    {
        var feature = await SeedFeatureAsync("no-tasks", PlanningFeatureStatus.InProgress);

        await CreateBackfill().RunAsync(default);

        var persisted = await ReloadAsync(feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.Ready);
        var line = PlanningLog.Parse(persisted.Body)
            .Single(l => l.Text == "data-fix: inProgress without a taken task → ready");
        line.Source.ShouldBe(PlanningLogSource.Auto);

        await _materializer.Received().SyncAsync(
            _projectId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_InProgressWithAllTodoTasks_MovesToReady()
    {
        var feature = await SeedFeatureAsync("all-todo", PlanningFeatureStatus.InProgress);
        await SeedTaskAsync(feature.Id, PlanningArtifactStatus.Todo);
        await SeedTaskAsync(feature.Id, PlanningArtifactStatus.Todo);

        await CreateBackfill().RunAsync(default);

        (await ReloadAsync(feature.Id)).Status.ShouldBe(PlanningFeatureStatus.Ready);
    }

    [Theory]
    [InlineData(PlanningArtifactStatus.InProgress)]
    [InlineData(PlanningArtifactStatus.Review)]
    [InlineData(PlanningArtifactStatus.Done)]
    public async Task RunAsync_InProgressWithTakenTask_FeatureStays(PlanningArtifactStatus takenStatus)
    {
        var feature = await SeedFeatureAsync($"taken-{takenStatus}", PlanningFeatureStatus.InProgress);
        await SeedTaskAsync(feature.Id, PlanningArtifactStatus.Todo);
        await SeedTaskAsync(feature.Id, takenStatus);

        await CreateBackfill().RunAsync(default);

        var persisted = await ReloadAsync(feature.Id);
        persisted.Status.ShouldBe(PlanningFeatureStatus.InProgress);
        PlanningLog.Parse(persisted.Body)
            .ShouldNotContain(l => l.Text.StartsWith("data-fix"));
    }

    [Fact]
    public async Task RunAsync_NonInProgressFeatures_Untouched()
    {
        var draft = await SeedFeatureAsync("draft", PlanningFeatureStatus.Draft);
        var ready = await SeedFeatureAsync("ready", PlanningFeatureStatus.Ready);
        var review = await SeedFeatureAsync("review", PlanningFeatureStatus.Review);
        var done = await SeedFeatureAsync("done", PlanningFeatureStatus.Done);

        await CreateBackfill().RunAsync(default);

        (await ReloadAsync(draft.Id)).Status.ShouldBe(PlanningFeatureStatus.Draft);
        (await ReloadAsync(ready.Id)).Status.ShouldBe(PlanningFeatureStatus.Ready);
        (await ReloadAsync(review.Id)).Status.ShouldBe(PlanningFeatureStatus.Review);
        (await ReloadAsync(done.Id)).Status.ShouldBe(PlanningFeatureStatus.Done);
    }

    [Fact]
    public async Task RunAsync_AlreadyStamped_LeavesNewLiarBehind()
    {
        await SeedFeatureAsync("first-wave", PlanningFeatureStatus.InProgress);
        await CreateBackfill().RunAsync(default);

        var late = await SeedFeatureAsync("late-liar", PlanningFeatureStatus.InProgress);
        await CreateBackfill().RunAsync(default);

        (await ReloadAsync(late.Id)).Status.ShouldBe(PlanningFeatureStatus.InProgress);
        (await _settings.GetAsync(PlanningStatusBackfill.VersionSettingKey, default)).ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_DeletedTakenTask_DoesNotKeepFeatureInProgress()
    {
        var feature = await SeedFeatureAsync("deleted-task", PlanningFeatureStatus.InProgress);
        var task = await SeedTaskAsync(feature.Id, PlanningArtifactStatus.Done);
        task.Delete(byHuman: true);
        await _context.SaveChangesAsync();

        await CreateBackfill().RunAsync(default);

        (await ReloadAsync(feature.Id)).Status.ShouldBe(PlanningFeatureStatus.Ready);
    }

    public async Task InitializeAsync() =>
        await TestPlanningDispatcher.EnableSyncModeAsync(_context);

    public Task DisposeAsync() => Task.CompletedTask;

    private PlanningStatusBackfill CreateBackfill() =>
        new(
            _context,
            _settings,
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningStatusBackfill>.Instance);

    private async Task<PlanningFeature> SeedFeatureAsync(string slug, PlanningFeatureStatus status)
    {
        var feature = PlanningFeature.Create(_projectId, slug, slug, string.Empty);
        feature.SetStatus(status);
        _context.PlanningFeatures.Add(feature);
        await _context.SaveChangesAsync();

        return feature;
    }

    private async Task<PlanningArtifact> SeedTaskAsync(Guid featureId, PlanningArtifactStatus status)
    {
        var order = await _context.PlanningArtifacts
            .Where(a => a.FeatureId == featureId && a.Kind == PlanningArtifactKind.Task)
            .CountAsync();
        var task = PlanningArtifact.Create(
            featureId, PlanningArtifactKind.Task, $"task {order}", order, PlanningReviewLevel.Light, null);
        task.SetStatus(status, "seeded", PlanningLogSource.Agent);
        _context.PlanningArtifacts.Add(task);
        await _context.SaveChangesAsync();

        return task;
    }

    private async Task<PlanningFeature> ReloadAsync(Guid featureId)
    {
        _context.ChangeTracker.Clear();

        return await _context.PlanningFeatures.FirstAsync(f => f.Id == featureId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }
}
