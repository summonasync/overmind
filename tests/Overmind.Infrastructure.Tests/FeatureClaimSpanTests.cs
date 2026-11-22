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

public sealed class FeatureClaimSpanTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public FeatureClaimSpanTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PlanningMaterializationReport(
                Guid.Empty, PlanningMaterialization.Off, [], [], []));

        var project = Project.Create($"test-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private FeatureClaimService CreateClaimService() =>
        new(_context, TestPlanningDispatcher.Create(_context, _materializer));

    private PlanningFeatureService CreateFeatureService() =>
        new(
            _context,
            _materializer,
            new BacklogImportSeeder(_context, new PlanningPolicy(_context, new AppSettingsService(_context))),
            new TaskDependencyService(_context, TestPlanningDispatcher.Create(_context, _materializer)),
            TestPlanningDispatcher.Create(_context, _materializer),
            NullLogger<PlanningFeatureService>.Instance);

    private async Task<PlanningFeature> CreateFeatureAsync(string slug = "feat-a") =>
        await CreateFeatureService().CreateAsync(_projectId, slug, "Title", "body");

    private async Task<List<FeatureClaimSpan>> OrderedSpansAsync(Guid featureId) =>
        await _context.FeatureClaimSpans.AsNoTracking()
            .Where(s => s.FeatureId == featureId)
            .OrderBy(s => s.ClaimedAt)
            .ToListAsync();

    private async Task AgeHolderSessionAsync(DateTime lastSeenAt, DateTime? endedAt = null)
    {
        var session = _context.AgentSessions.Single(s => s.Id == HolderSessionId);
        _context.AgentSessions.Remove(session);
        _context.AgentSessions.Add(AgentSession.Restore(
            HolderSessionId, "Dan", "qwen", lastSeenAt.AddMinutes(-30), lastSeenAt, endedAt));
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task Claim_SequentialClaimsByTwoAgents_LeavesTwoSpansWithFirstClosed()
    {
        var service = CreateClaimService();
        var feature = await CreateFeatureAsync();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await service.UnclaimAsync(_projectId, "feat-a", HolderSessionId);

        await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);

        var spans = await OrderedSpansAsync(feature.Id);
        spans.Count.ShouldBe(2);
        spans[0].Alias.ShouldBe("Dan");
        spans[0].SessionId.ShouldBe(HolderSessionId);
        spans[0].ReleasedAt.ShouldNotBeNull();
        spans[1].Alias.ShouldBe("Bobby");
        spans[1].SessionId.ShouldBe(ForeignSessionId);
        spans[1].ReleasedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Claim_TakeOverOfStaleHolder_ClosesTheOldHolderSpan()
    {
        var service = CreateClaimService();
        var feature = await CreateFeatureAsync();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);

        var spans = await OrderedSpansAsync(feature.Id);
        spans.Count.ShouldBe(2);
        spans[0].SessionId.ShouldBe(HolderSessionId);
        spans[0].ReleasedAt.ShouldNotBeNull();
        spans[0].ReleasedAt.Value.ShouldBeGreaterThanOrEqualTo(spans[0].ClaimedAt);
        spans[1].SessionId.ShouldBe(ForeignSessionId);
        spans[1].ReleasedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Claim_SameSessionRefresh_KeepsTheOpenSpanSingle()
    {
        var service = CreateClaimService();
        var feature = await CreateFeatureAsync();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var spans = await OrderedSpansAsync(feature.Id);
        spans.Count.ShouldBe(1);
        spans[0].SessionId.ShouldBe(HolderSessionId);
        spans[0].ReleasedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Unclaim_ByHolder_ClosesTheOpenSpan()
    {
        var service = CreateClaimService();
        var feature = await CreateFeatureAsync();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        await service.UnclaimAsync(_projectId, "feat-a", HolderSessionId);

        var spans = await OrderedSpansAsync(feature.Id);
        spans.Count.ShouldBe(1);
        spans[0].ReleasedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SetStatus_DoneClose_ClosesTheOpenSpan()
    {
        var feature = await CreateFeatureAsync();
        await CreateClaimService().ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        await _context.SaveChangesAsync();
        await SeedResultWithTapAsync("feat-a");

        await CreateFeatureService().SetStatusAsync(
            _projectId, "feat-a", PlanningFeatureStatus.Done, "closed by user request",
            requireHumanTap: true, ct: default);

        var spans = await OrderedSpansAsync(feature.Id);
        spans.Count.ShouldBe(1);
        spans[0].ReleasedAt.ShouldNotBeNull();
    }

    private async Task SeedResultWithTapAsync(string slug)
    {
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
        _context.PlanningArtifacts.Add(
            PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out"));
        var notification = AgentNotification.Create(
            NotificationKind.Approval, "please approve the close", _projectId, slug);
        notification.Reply("lgtm");
        _context.AgentNotifications.Add(notification);
        await _context.SaveChangesAsync();
    }
}
