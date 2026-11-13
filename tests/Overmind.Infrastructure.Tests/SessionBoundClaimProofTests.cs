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

// The injected clock: services evaluate DateTime.UtcNow, so explicit past timestamps on the
// session row place the scenario minutes or hours ahead of the claim without waiting.
public sealed class SessionBoundClaimProofTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public SessionBoundClaimProofTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"proof-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.AgentSessions.Add(AgentSession.Restore(
            HolderSessionId, "Dan", "qwen", DateTime.UtcNow, DateTime.UtcNow));
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Claim_ReadOnlyHolderAcrossLegacyWindow_StayAliveWithZeroBoardMutations()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await SilenceBoardAsync(hours: 2);
        await TouchHolderAsync(minutesAgo: 1, sessionAgeHours: 2);

        var liveness = await service.ClaimLivenessAsync(
            _projectId, await ClaimedFeaturesAsync());

        liveness.Values.ShouldAllBe(l => l == AgentSessionLiveness.Alive);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("(alive)");
        PlanningLog.Parse(await FeatureBodyAsync()).Last().Text
            .ShouldBe("Dan by qwen claimed the feature");
    }

    [Fact]
    public async Task Claim_HeartbeatOnlyHolderAcrossHours_StayAlive()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await SilenceBoardAsync(hours: 3);
        await TouchHolderAsync(minutesAgo: 4, sessionAgeHours: 3);

        var liveness = await service.ClaimLivenessAsync(
            _projectId, await ClaimedFeaturesAsync());

        liveness.Values.ShouldAllBe(l => l == AgentSessionLiveness.Alive);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("(alive)");
    }

    [Fact]
    public async Task ClaimLiveness_EndedHolder_ReportsReleasedAndTakeOverDoorsUnchanged()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await TouchHolderAsync(minutesAgo: 30, sessionAgeHours: 1, endedAt: DateTime.UtcNow.AddMinutes(-20));

        var liveness = await service.ClaimLivenessAsync(
            _projectId, await ClaimedFeaturesAsync());

        liveness.Values.ShouldAllBe(l => l == AgentSessionLiveness.Released);

        var taken = await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);
        PlanningLog.Parse(taken.Body).Last().Text
            .ShouldBe("Bobby by claude took over ended claim of Dan by qwen");
    }

    [Fact]
    public async Task ClaimLiveness_SilentHolder_TurnsStaleAndYieldsToTakeOver()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await TouchHolderAsync(minutesAgo: 60, sessionAgeHours: 2);

        var liveness = await service.ClaimLivenessAsync(
            _projectId, await ClaimedFeaturesAsync());

        liveness.Values.ShouldAllBe(l => l == AgentSessionLiveness.Stale);

        var taken = await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);
        PlanningLog.Parse(taken.Body).Last().Text
            .ShouldBe("Bobby by claude took over stale claim of Dan by qwen");
    }

    [Fact]
    public async Task WaveSnapshot_ReadOnlyHolderAfterLongSilence_ProjectsAliveClaim()
    {
        await CreateFeatureAsync("feat-live", PlanningFeatureStatus.InProgress);
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-live", "Dan", "qwen", HolderSessionId);
        await SilenceBoardAsync(hours: 2, slug: "feat-live");
        await TouchHolderAsync(minutesAgo: 1, sessionAgeHours: 2);

        var snapshot = await CreateSnapshotService().SnapshotAsync(_projectId);

        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-live");
        snapshot.ClaimLiveness[feature.Id].ShouldBe(AgentSessionLiveness.Alive);
    }

    private FeatureClaimService CreateClaimService() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    private WaveSnapshotService CreateSnapshotService() =>
        new(_context, CreateClaimService(), new DirectiveService(_context));

    private async Task<PlanningFeature> CreateFeatureAsync(
        string slug = "feat-a", PlanningFeatureStatus? status = null)
    {
        var service = new PlanningFeatureService(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);
        await service.CreateAsync(_projectId, slug, slug, string.Empty);

        if (status is PlanningFeatureStatus.InProgress)
        {
            await service.SetStatusAsync(_projectId, slug, PlanningFeatureStatus.InProgress, "proof move");
        }

        return await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
    }

    private async Task SilenceBoardAsync(int hours, string slug = "feat-a")
    {
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
        _context.Entry(feature).Property(f => f.ClaimedAt).CurrentValue =
            DateTime.UtcNow.AddHours(-hours);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task TouchHolderAsync(int minutesAgo, int sessionAgeHours, DateTime? endedAt = null)
    {
        var session = await _context.AgentSessions.SingleAsync(s => s.Id == HolderSessionId);
        _context.AgentSessions.Remove(session);
        _context.AgentSessions.Add(AgentSession.Restore(
            HolderSessionId,
            "Dan",
            "qwen",
            DateTime.UtcNow.AddHours(-sessionAgeHours),
            DateTime.UtcNow.AddMinutes(-minutesAgo),
            endedAt));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task<List<PlanningFeature>> ClaimedFeaturesAsync() =>
        await _context.PlanningFeatures.Where(f => f.ClaimSessionId != null).ToListAsync();

    private async Task<string> FeatureBodyAsync()
    {
        _context.ChangeTracker.Clear();
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        return feature.Body;
    }
}
