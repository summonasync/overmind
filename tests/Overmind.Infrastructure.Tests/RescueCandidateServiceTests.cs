using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class RescueCandidateServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly Guid _projectId;
    private readonly RescueCandidateService _service;

    public RescueCandidateServiceTests()
    {
        _context = _db.CreateContext();
        var project = Project.Create($"rescue-{Guid.NewGuid():N}", Path.GetTempPath(), string.Empty);
        _context.Projects.Add(project);
        _context.SaveChanges();
        _projectId = project.Id;
        _service = new RescueCandidateService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task ListAsync_StaleAndEndedClaimsInActiveStatuses_KeepsOldestAbandonmentFirst()
    {
        var aliveSeen = DateTime.UtcNow;
        var staleSeen = DateTime.UtcNow.AddHours(-2);

        Claim(SeedFeature("f-alive", PlanningFeatureStatus.InProgress),
            SeedSession(Guid.NewGuid(), aliveSeen));
        Claim(SeedFeature("f-ready", PlanningFeatureStatus.Ready),
            SeedSession(Guid.NewGuid(), staleSeen), claimedAt: DateTime.UtcNow.AddHours(-4));
        Claim(SeedFeature("f-stale", PlanningFeatureStatus.InProgress),
            SeedSession(Guid.NewGuid(), staleSeen), claimedAt: DateTime.UtcNow.AddHours(-3));
        Claim(SeedFeature("f-ended", PlanningFeatureStatus.Review),
            SeedSession(Guid.NewGuid(), staleSeen, endedAt: DateTime.UtcNow.AddHours(-1)),
            claimedAt: DateTime.UtcNow.AddHours(-2));
        SeedFeature("f-unclaimed", PlanningFeatureStatus.InProgress);
        Claim(SeedFeature("f-draft", PlanningFeatureStatus.Draft), SeedSession(Guid.NewGuid(), staleSeen));

        var candidates = await _service.ListAsync(_projectId);

        candidates.Select(c => c.Slug).ShouldBe(["f-stale", "f-ended"]);
        candidates[0].ClaimState.ShouldBe(AgentSessionLiveness.Stale);
        candidates[1].ClaimState.ShouldBe(AgentSessionLiveness.Ended);
    }

    [Fact]
    public async Task ListAsync_MissingSessionRow_CountsAsEnded()
    {
        var feature = SeedFeature("f-ghost", PlanningFeatureStatus.InProgress);
        feature.Claim("dan", "qwen", Guid.NewGuid(), DateTime.UtcNow.AddHours(-5));
        _context.SaveChanges();

        var candidates = await _service.ListAsync(_projectId);

        candidates.ShouldHaveSingleItem();
        candidates[0].ClaimState.ShouldBe(AgentSessionLiveness.Ended);
    }

    [Fact]
    public async Task ListAsync_OpenSpanProvenance_WinsOverFeatureColumns()
    {
        var feature = SeedFeature("f-span", PlanningFeatureStatus.InProgress);
        var session = SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddHours(-2));
        feature.Claim("column-dan", "qwen", session.Id, DateTime.UtcNow.AddHours(-1));
        _context.FeatureClaimSpans.Add(FeatureClaimSpan.Open(
            _projectId, feature.Id, "ledger-dan", "claude", session.Id, DateTime.UtcNow.AddHours(-6)));
        _context.SaveChanges();

        var candidates = await _service.ListAsync(_projectId);

        var row = candidates.ShouldHaveSingleItem();
        row.ClaimedByAlias.ShouldBe("ledger-dan");
        row.ClaimedByVendor.ShouldBe("claude");
        row.ClaimedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddHours(-5));
    }

    [Fact]
    public async Task ListAsync_NoOpenSpan_FallsBackToFeatureColumns()
    {
        var feature = SeedFeature("f-nospan", PlanningFeatureStatus.Review);
        var session = SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddHours(-3), endedAt: DateTime.UtcNow.AddHours(-2));
        feature.Claim("column-dan", "qwen", session.Id, DateTime.UtcNow.AddHours(-4));
        _context.SaveChanges();

        var candidates = await _service.ListAsync(_projectId);

        var row = candidates.ShouldHaveSingleItem();
        row.ClaimedByAlias.ShouldBe("column-dan");
        row.ClaimedByVendor.ShouldBe("qwen");
        row.ClaimedAt.ShouldBeGreaterThanOrEqualTo(DateTime.UtcNow.AddHours(-4).AddMinutes(-1));
        row.ClaimedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddHours(-4).AddMinutes(1));
    }

    [Fact]
    public async Task ListAsync_CarriesCodePinAndUpdatedAt()
    {
        var feature = SeedFeature("f-pin", PlanningFeatureStatus.InProgress);
        feature.SetCodePin("feat/235-abandoned", "/worktrees/235", PlanningLogSource.Agent);
        Claim(feature, SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddHours(-2)));

        var candidates = await _service.ListAsync(_projectId);

        var row = candidates.ShouldHaveSingleItem();
        row.PinnedBranch.ShouldBe("feat/235-abandoned");
        row.PinnedWorktreePath.ShouldBe("/worktrees/235");
        row.UpdatedAt.ShouldBeGreaterThanOrEqualTo(DateTime.UtcNow.AddMinutes(-5));
        row.UpdatedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task ListAsync_UnknownProject_ThrowsPlanningException()
    {
        await Should.ThrowAsync<PlanningException>(() => _service.ListAsync(Guid.NewGuid()));
    }

    private PlanningFeature SeedFeature(string slug, PlanningFeatureStatus status)
    {
        var feature = PlanningFeature.Create(_projectId, slug, slug, "body");
        feature.SetStatus(status);
        _context.PlanningFeatures.Add(feature);
        return feature;
    }

    private AgentSession SeedSession(Guid id, DateTime lastSeenAt, DateTime? endedAt = null)
    {
        var session = AgentSession.Restore(id, "dan", "qwen", lastSeenAt.AddHours(-1), lastSeenAt, endedAt);
        _context.AgentSessions.Add(session);
        return session;
    }

    private void Claim(PlanningFeature feature, AgentSession session, DateTime? claimedAt = null)
    {
        feature.Claim(session.Alias, session.Vendor, session.Id, claimedAt ?? DateTime.UtcNow.AddHours(-3));
        _context.SaveChanges();
    }
}
