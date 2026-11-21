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

public sealed class FeatureClaimServiceTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private readonly TempDatabase _db = new();
    private readonly OvermindDbContext _context;
    private readonly IPlanningMaterializer _materializer;
    private readonly Guid _projectId;

    public FeatureClaimServiceTests()
    {
        _context = _db.CreateContext();
        _materializer = Substitute.For<IPlanningMaterializer>();
        _materializer.SyncAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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

    [Fact]
    public async Task LiveSurfacesForClaim_LaneWatchesOverlappingGlobs_ListsThem()
    {
        var service = CreateClaimService();
        await SeedWatchedLaneAsync(
            "web-boundary",
            new LiveSurfaceEntry("web/src/**", "van-darkholme"),
            new LiveSurfaceEntry("docs/**", "billy-herrington"));
        var feature = await CreateFeatureInLaneAsync("web-boundary", ["src/Overmind.Host/**", "web/src/**"]);

        var live = await service.LiveSurfacesForClaimAsync(feature);

        live.ShouldBe([new LiveSurfaceEntry("web/src/**", "van-darkholme")]);
    }

    [Fact]
    public async Task LiveSurfacesForClaim_NoOverlap_ReturnsEmpty()
    {
        var service = CreateClaimService();
        await SeedWatchedLaneAsync("web-boundary", new LiveSurfaceEntry("web/src/**", "van-darkholme"));
        var feature = await CreateFeatureInLaneAsync("web-boundary", ["src/api/**/*.cs"]);

        var live = await service.LiveSurfacesForClaimAsync(feature);

        live.ShouldBe([]);
    }

    [Fact]
    public async Task LiveSurfacesForClaim_FeatureWithoutCodeSurfaces_ReturnsEmptyWithoutError()
    {
        var service = CreateClaimService();
        await SeedWatchedLaneAsync("web-boundary", new LiveSurfaceEntry("web/src/**", "van-darkholme"));
        var feature = await CreateFeatureInLaneAsync("web-boundary");

        var live = await service.LiveSurfacesForClaimAsync(feature);

        live.ShouldBe([]);
    }

    [Fact]
    public async Task ReattributeSessionClaims_RenamedHolder_MovesSnapshotsLogsAndReopensTheSpan()
    {
        var service = CreateClaimService();
        var held = await CreateFeatureAsync("feat-held");
        var foreign = await CreateFeatureAsync("feat-other");
        var foreignSession = await AddSessionAsync("Van", "claude", DateTime.UtcNow);

        await service.ClaimAsync(_projectId, "feat-held", "agent", "unknown", HolderSessionId);
        await service.ClaimAsync(_projectId, "feat-other", "Van", "claude", foreignSession);
        var span = _context.FeatureClaimSpans.Single(s => s.FeatureId == held.Id);
        var spanId = span.Id;
        var holdStart = span.ClaimedAt;
        var claimStamp = held.ClaimedAt;

        var moved = await service.ReattributeSessionClaimsAsync(HolderSessionId, "mark-wolff", "claude");

        moved.Select(m => m.Slug).ShouldBe(["feat-held"]);
        held.ClaimedByAlias.ShouldBe("mark-wolff");
        held.ClaimedByVendor.ShouldBe("claude");
        held.ClaimSessionId.ShouldBe(HolderSessionId);
        held.ClaimedAt.ShouldBe(claimStamp);
        foreign.ClaimedByAlias.ShouldBe("Van");

        PlanningLog.Parse(held.Body).ShouldContain(line =>
            line.Text == "claim re-attributed: agent by unknown → mark-wolff by claude");

        var closed = _context.FeatureClaimSpans.AsNoTracking().Single(s => s.Id == spanId);
        closed.ReleasedAt.ShouldNotBeNull();
        closed.Alias.ShouldBe("agent");

        var reopened = _context.FeatureClaimSpans.AsNoTracking()
            .Single(s => s.FeatureId == held.Id && s.ReleasedAt == null);
        reopened.FeatureId.ShouldBe(held.Id);
        reopened.Alias.ShouldBe("mark-wolff");
        reopened.Vendor.ShouldBe("claude");
        reopened.SessionId.ShouldBe(HolderSessionId);
        reopened.ClaimedAt.ShouldBe(holdStart);

        var foreignOpen = _context.FeatureClaimSpans.AsNoTracking()
            .Single(s => s.FeatureId == foreign.Id && s.ReleasedAt == null);
        foreignOpen.Alias.ShouldBe("Van");
        _context.FeatureClaimSpans.AsNoTracking().Count(s => s.ReleasedAt != null).ShouldBe(1);
    }

    [Fact]
    public async Task ReattributeSessionClaims_SamePair_LeavesTheLedgerAlone()
    {
        var service = CreateClaimService();
        var held = await CreateFeatureAsync();
        await service.ClaimAsync(_projectId, "feat-a", "mark-wolff", "claude", HolderSessionId);
        var spansBefore = await _context.FeatureClaimSpans.CountAsync();

        var moved = await service.ReattributeSessionClaimsAsync(HolderSessionId, "mark-wolff", "claude");

        moved.ShouldBeEmpty();
        var spansAfter = await _context.FeatureClaimSpans.CountAsync();
        spansAfter.ShouldBe(spansBefore);
        PlanningLog.Parse(held.Body).ShouldNotContain(line =>
            line.Text.StartsWith("claim re-attributed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReattributeSessionClaims_NoClaimsHeld_ReturnsEmpty()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();

        var moved = await service.ReattributeSessionClaimsAsync(Guid.NewGuid(), "mark-wolff", "claude");

        moved.ShouldBeEmpty();
    }

    private async Task<PlanningFeature> CreateFeatureAsync(string slug = "feat-a") =>
        await CreateFeatureService().CreateAsync(_projectId, slug, "Title", "body");

    private async Task<PlanningFeature> CreateFeatureInLaneAsync(string lane, string[]? codeSurfaces = null)
    {
        var feature = await CreateFeatureAsync();
        feature.SetLane(lane);
        if (codeSurfaces is not null)
        {
            feature.SetCodeSurfaces(CodeSurfacesJson.Serialize(codeSurfaces));
        }

        await _context.SaveChangesAsync();
        return feature;
    }

    private async Task SeedWatchedLaneAsync(string name, params LiveSurfaceEntry[] surfaces)
    {
        _context.PlanningLanes.Add(PlanningLane.Create(
            _projectId, name, null, null, null, LiveSurfacesJson.Serialize(surfaces)));
        await _context.SaveChangesAsync();
    }

    private async Task AgeHolderSessionAsync(DateTime lastSeenAt, DateTime? endedAt = null)
    {
        var session = _context.AgentSessions.Single(s => s.Id == HolderSessionId);
        _context.AgentSessions.Remove(session);
        _context.AgentSessions.Add(AgentSession.Restore(
            HolderSessionId, "Dan", "qwen", lastSeenAt.AddMinutes(-30), lastSeenAt, endedAt));
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> AddSessionAsync(string alias, string vendor, DateTime lastSeenAt)
    {
        var session = AgentSession.Restore(Guid.NewGuid(), alias, vendor, lastSeenAt, lastSeenAt);
        _context.AgentSessions.Add(session);
        await _context.SaveChangesAsync();
        return session.Id;
    }

    [Fact]
    public async Task Claim_FreeFeature_ClaimsAndLogs()
    {
        var feature = await CreateFeatureAsync();
        var service = CreateClaimService();

        var claimed = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        claimed.ClaimedByAlias.ShouldBe("Dan");
        claimed.ClaimedByVendor.ShouldBe("qwen");
        claimed.ClaimSessionId.ShouldBe(HolderSessionId);
        claimed.ClaimedAt.ShouldNotBeNull();
        claimed.Status.ShouldBe(PlanningFeatureStatus.Draft);
        PlanningLog.Parse(claimed.Body).Last().Text.ShouldBe("Dan by qwen claimed the feature");

        var session = await _context.AgentSessions.SingleAsync(s => s.Id == HolderSessionId);
        session.Alias.ShouldBe("Dan");
        session.Vendor.ShouldBe("qwen");
    }

    [Fact]
    public async Task Claim_DoneFeature_Throws()
    {
        await CreateFeatureAsync();
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        feature.SetStatus(PlanningFeatureStatus.Done);
        await _context.SaveChangesAsync();

        var ex = await Should.ThrowAsync<PlanningException>(() => CreateClaimService()
            .ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimRefused);
    }

    [Fact]
    public async Task Claim_LiveForeignSession_ThrowsNamingHolder()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("claim held by Dan by qwen (alive)");
        ex.Message.ShouldContain("feat-a");
    }

    [Fact]
    public async Task Claim_LiveSessionSameAliasVendor_StillThrows()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-a", "Dan", "qwen", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
    }

    [Fact]
    public async Task Claim_SameLiveSession_IsIdempotent()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        var claimedAt = (await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a")).ClaimedAt;

        var again = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        again.ClaimSessionId.ShouldBe(HolderSessionId);
        again.ClaimedAt.ShouldBe(claimedAt);
        PlanningLog.Parse(again.Body).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Claim_StaleForeignSession_TakesOverAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        var taken = await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);

        taken.ClaimSessionId.ShouldBe(ForeignSessionId);
        taken.ClaimedByAlias.ShouldBe("Bobby");
        PlanningLog.Parse(taken.Body).Last().Text
            .ShouldBe("Bobby by claude took over stale claim of Dan by qwen");
    }

    [Fact]
    public async Task Claim_EndedForeignSession_TakesOverAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));

        var taken = await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);

        taken.ClaimSessionId.ShouldBe(ForeignSessionId);
        PlanningLog.Parse(taken.Body).Last().Text
            .ShouldBe("Bobby by claude took over ended claim of Dan by qwen");
    }

    [Fact]
    public async Task Claim_EndedSessionSameAliasVendor_AdoptsAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        var restartedSessionId = Guid.NewGuid();
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));

        var adopted = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", restartedSessionId);

        adopted.ClaimSessionId.ShouldBe(restartedSessionId);
        adopted.ClaimedByAlias.ShouldBe("Dan");
        PlanningLog.Parse(adopted.Body).Last().Text
            .ShouldBe("session restarted, claim adopted by Dan by qwen");
    }

    [Fact]
    public async Task Claim_StaleSameSession_RefreshesSilently()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        var refreshed = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        refreshed.ClaimedAt.ShouldNotBeNull();
        PlanningLog.Parse(refreshed.Body).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Claim_ZeroIdentityParams_BindsTheSessionIdentity()
    {
        await CreateFeatureAsync();
        var callerId = await AddSessionAsync("Tucker", "qwen", DateTime.UtcNow);

        var claimed = await CreateClaimService().ClaimAsync(_projectId, "feat-a", null, null, callerId);

        claimed.ClaimedByAlias.ShouldBe("Tucker");
        claimed.ClaimedByVendor.ShouldBe("qwen");
        claimed.ClaimSessionId.ShouldBe(callerId);
        PlanningLog.Parse(claimed.Body).Last().Text.ShouldBe("Tucker by qwen claimed the feature");
    }

    [Fact]
    public async Task Claim_VendorOnly_FillsAliasFromTheSession()
    {
        await CreateFeatureAsync();
        var callerId = await AddSessionAsync("Dan", "qwen", DateTime.UtcNow);

        var claimed = await CreateClaimService().ClaimAsync(_projectId, "feat-a", "Dan", null, callerId);

        claimed.ClaimedByAlias.ShouldBe("Dan");
        claimed.ClaimedByVendor.ShouldBe("qwen");
    }

    [Fact]
    public async Task Claim_PlaceholderAliasSession_UpgradeSessionToClaimAlias()
    {
        await CreateFeatureAsync();
        var callerId = await AddSessionAsync(AgentSession.PlaceholderAlias, "unknown", DateTime.UtcNow);

        var claimed = await CreateClaimService().ClaimAsync(_projectId, "feat-a", "Guldan", "qwen", callerId);

        claimed.ClaimedByAlias.ShouldBe("Guldan");
        var session = await _context.AgentSessions.AsNoTracking().SingleAsync(s => s.Id == callerId);
        session.Alias.ShouldBe("Guldan");
        session.Vendor.ShouldBe("unknown");
    }

    [Fact]
    public async Task Claim_RealAliasSession_NeverRewritesTheIdentity()
    {
        await CreateFeatureAsync();
        var callerId = await AddSessionAsync("Tucker", "qwen", DateTime.UtcNow);

        var claimed = await CreateClaimService().ClaimAsync(_projectId, "feat-a", "Guldan", "qwen", callerId);

        claimed.ClaimedByAlias.ShouldBe("Guldan");
        var session = await _context.AgentSessions.AsNoTracking().SingleAsync(s => s.Id == callerId);
        session.Alias.ShouldBe("Tucker");
    }

    [Fact]
    public async Task Claim_StalePlaceholderClaim_TakeOverRenamesTheFreshSession()
    {
        await CreateFeatureAsync();
        var staleHolderId = await AddSessionAsync(AgentSession.PlaceholderAlias, "unknown", DateTime.UtcNow);
        await CreateClaimService().ClaimAsync(_projectId, "feat-a", "Guldan", "qwen", staleHolderId);
        _context.ChangeTracker.Clear();
        var holder = await _context.AgentSessions.SingleAsync(s => s.Id == staleHolderId);
        _context.AgentSessions.Remove(holder);
        _context.AgentSessions.Add(AgentSession.Restore(
            staleHolderId, "Guldan", "qwen",
            DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-90)));
        await _context.SaveChangesAsync();
        var freshSessionId = await AddSessionAsync(AgentSession.PlaceholderAlias, "unknown", DateTime.UtcNow);

        var taken = await CreateClaimService().ClaimAsync(_projectId, "feat-a", "Bobby", "claude", freshSessionId);

        taken.ClaimedByAlias.ShouldBe("Bobby");
        var fresh = await _context.AgentSessions.AsNoTracking().SingleAsync(s => s.Id == freshSessionId);
        fresh.Alias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Claim_UnregisteredSessionWithZeroIdentity_Throws()
    {
        await CreateFeatureAsync();

        var ex = await Should.ThrowAsync<ArgumentException>(() => CreateClaimService()
            .ClaimAsync(_projectId, "feat-a", null, null, Guid.NewGuid()));

        ex.Message.ShouldContain("not registered");
    }

    [Fact]
    public async Task Claim_HumanDoorWithoutIdentity_Throws()
    {
        await CreateFeatureAsync();

        var ex = await Should.ThrowAsync<ArgumentException>(() => CreateClaimService()
            .ClaimAsync(_projectId, "feat-a", null, null, null));

        ex.Message.ShouldContain("human door");
    }

    [Fact]
    public async Task Claim_ZeroThreshold_DisablesStale()
    {
        await CreateFeatureAsync();
        var project = await _context.Projects.SingleAsync(p => p.Id == _projectId);
        project.SetClaimSettings(0, null);
        await _context.SaveChangesAsync();

        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-6));

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("(alive)");
    }

    [Fact]
    public async Task Claim_MissingFeature_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateClaimService()
            .ClaimAsync(_projectId, "nonexistent", "Dan", "qwen", HolderSessionId));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task Claim_MissingProject_ThrowsNotFound()
    {
        var ex = await Should.ThrowAsync<PlanningException>(() => CreateClaimService()
            .ClaimAsync(Guid.NewGuid(), "feat-a", "Dan", "qwen", HolderSessionId));

        ex.Kind.ShouldBe(PlanningFailureKind.NotFound);
    }

    [Fact]
    public async Task HumanClaim_ThenAgentTakeOver_ClearsHumanFlag()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();

        await service.ClaimAsync(_projectId, "feat-a", "board", "human", null);
        var humanClaimed = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        humanClaimed.ClaimedByHuman.ShouldBeTrue();

        var humanSessionId = humanClaimed.ClaimSessionId!.Value;
        var humanSession = await _context.AgentSessions.SingleAsync(s => s.Id == humanSessionId);
        _context.AgentSessions.Remove(humanSession);
        _context.AgentSessions.Add(AgentSession.Restore(
            humanSessionId, "board", "human",
            DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2)));
        await _context.SaveChangesAsync();

        var taken = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        taken.ClaimedByAlias.ShouldBe("Dan");
        taken.ClaimedByHuman.ShouldBeFalse();
    }

    [Fact]
    public async Task Unclaim_ByHolder_ReleasesAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var released = await service.UnclaimAsync(_projectId, "feat-a", HolderSessionId);

        released.ClaimSessionId.ShouldBeNull();
        released.ClaimedByAlias.ShouldBeNull();
        PlanningLog.Parse(released.Body).Last().Text.ShouldBe("Dan by qwen released the claim");
    }

    [Fact]
    public async Task Unclaim_ByForeignSession_ThrowsNamingHolder()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.UnclaimAsync(_projectId, "feat-a", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("Dan by qwen");
    }

    [Fact]
    public async Task Unclaim_UnclaimedFeature_IsNoOp()
    {
        await CreateFeatureAsync();

        var feature = await CreateClaimService().UnclaimAsync(_projectId, "feat-a", HolderSessionId);

        feature.ClaimSessionId.ShouldBeNull();
        PlanningLog.Parse(feature.Body).ShouldBeEmpty();
    }

    [Fact]
    public async Task EnsureHolder_UnclaimedFeature_AllowsAnySession()
    {
        await CreateFeatureAsync();

        await CreateClaimService().EnsureHolderAsync(_projectId, "feat-a", ForeignSessionId);
    }

    [Fact]
    public async Task EnsureHolder_NullSession_HumanDoor_IsNeverLocked()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        await service.EnsureHolderAsync(_projectId, "feat-a", null);
    }

    [Fact]
    public async Task EnsureHolder_HolderSession_Passes()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        await service.EnsureHolderAsync(_projectId, "feat-a", HolderSessionId);
    }

    [Fact]
    public async Task EnsureHolder_ForeignSession_ThrowsNamingHolder()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EnsureHolderAsync(_projectId, "feat-a", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("Dan by qwen (alive)");
    }

    [Fact]
    public async Task EnsureHolder_StaleClaimOnSameSession_ReadoptsAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        await service.EnsureHolderAsync(_projectId, "feat-a", HolderSessionId);

        var readopted = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        readopted.ClaimSessionId.ShouldBe(HolderSessionId);
        readopted.ClaimedByAlias.ShouldBe("Dan");
        PlanningLog.Parse(readopted.Body).Last().Text
            .ShouldBe("stale claim re-adopted by resumed session Dan by qwen");
    }

    [Fact]
    public async Task EnsureHolder_EndedClaimOnSameSession_ReadoptsAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));

        await service.EnsureHolderAsync(_projectId, "feat-a", HolderSessionId);

        var readopted = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        readopted.ClaimSessionId.ShouldBe(HolderSessionId);
        PlanningLog.Parse(readopted.Body).Last().Text
            .ShouldBe("stale claim re-adopted by resumed session Dan by qwen");
    }

    [Fact]
    public async Task EnsureImportAllowed_StaleSameSessionClaim_ReadoptsAndLogs()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        await service.EnsureImportAllowedAsync(_projectId, HolderSessionId, "feat-a");

        var readopted = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        PlanningLog.Parse(readopted.Body).Last().Text
            .ShouldBe("stale claim re-adopted by resumed session Dan by qwen");
    }

    [Fact]
    public async Task EnsureHolder_ReadoptHealsTheRow_SecondWriteIsSilent()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));

        await service.EnsureHolderAsync(_projectId, "feat-a", HolderSessionId);
        await service.EnsureHolderAsync(_projectId, "feat-a", HolderSessionId);

        var healed = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        PlanningLog.Parse(healed.Body)
            .Count(line => line.Text.StartsWith("stale claim re-adopted", StringComparison.Ordinal))
            .ShouldBe(1);
        var row = await _context.AgentSessions.AsNoTracking()
            .SingleAsync(s => s.Id == HolderSessionId);
        row.EndedAt.ShouldBeNull();
        row.LastSeenAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task EnsureHolder_StaleSameAliasForeignSession_RequiresExplicitClaim()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));
        var callerId = await AddSessionAsync("Dan", "qwen", DateTime.UtcNow);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EnsureHolderAsync(_projectId, "feat-a", callerId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("Dan by qwen (stale)");
        var untouched = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        untouched.ClaimSessionId.ShouldBe(HolderSessionId);
        PlanningLog.Parse(untouched.Body).Single().Text.ShouldBe("Dan by qwen claimed the feature");
    }

    [Fact]
    public async Task EnsureHolder_AliveSelfIdentityClaim_ThrowsNamingHolder()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        var callerId = await AddSessionAsync("Dan", "qwen", DateTime.UtcNow);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EnsureHolderAsync(_projectId, "feat-a", callerId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("Dan by qwen (alive)");
        var untouched = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        untouched.ClaimSessionId.ShouldBe(HolderSessionId);
        PlanningLog.Parse(untouched.Body).Count.ShouldBe(1);
    }

    [Fact]
    public async Task EnsureHolder_StaleForeignClaim_RefusesWrites()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));
        var callerId = await AddSessionAsync("Bobby", "claude", DateTime.UtcNow);

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EnsureHolderAsync(_projectId, "feat-a", callerId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("Dan by qwen (stale)");
        var untouched = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        untouched.ClaimSessionId.ShouldBe(HolderSessionId);
        PlanningLog.Parse(untouched.Body).Single().Text.ShouldBe("Dan by qwen claimed the feature");
    }

    [Fact]
    public async Task EnsureHolder_UnregisteredCallerOnStaleClaim_StillRefuses()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        var ex = await Should.ThrowAsync<PlanningException>(
            () => service.EnsureHolderAsync(_projectId, "feat-a", Guid.NewGuid()));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("(stale)");
    }

    [Fact]
    public async Task ClaimLiveness_EndedHolderSession_ProjectsReleased()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));
        var feature = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat-a");

        var liveness = await service.ClaimLivenessAsync(_projectId, [feature]);

        liveness.GetValueOrDefault(feature.Id).ShouldBe(AgentSessionLiveness.Released);
    }

    [Fact]
    public async Task ClaimLiveness_MissingHolderSession_ProjectsReleased()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        var holder = await _context.AgentSessions.SingleAsync(s => s.Id == HolderSessionId);
        _context.AgentSessions.Remove(holder);
        await _context.SaveChangesAsync();
        var feature = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat-a");

        var liveness = await service.ClaimLivenessAsync(_projectId, [feature]);

        liveness.GetValueOrDefault(feature.Id).ShouldBe(AgentSessionLiveness.Released);
    }

    [Fact]
    public async Task ClaimLiveness_StaleHolder_ProjectsStale()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));
        var feature = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat-a");

        var liveness = await service.ClaimLivenessAsync(_projectId, [feature]);

        liveness.GetValueOrDefault(feature.Id).ShouldBe(AgentSessionLiveness.Stale);
    }

    [Fact]
    public async Task Claim_ReleasedClaim_ForeignTakeOverStillSucceeds()
    {
        await CreateFeatureAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));
        var feature = await _context.PlanningFeatures.AsNoTracking().SingleAsync(f => f.Slug == "feat-a");
        (await service.ClaimLivenessAsync(_projectId, [feature]))
            .GetValueOrDefault(feature.Id).ShouldBe(AgentSessionLiveness.Released);

        var taken = await service.ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId);

        taken.ClaimSessionId.ShouldBe(ForeignSessionId);
        taken.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Unclaim_AwaitingHumanFeature_KeepsThePhase()
    {
        var feature = await CreateFeatureAsync();
        feature.SetStatus(PlanningFeatureStatus.Review);
        var result = PlanningArtifact.Create(feature.Id, PlanningArtifactKind.Result, "close-out");
        result.Validate();
        _context.PlanningArtifacts.Add(result);
        await _context.SaveChangesAsync();
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        var inbox = new HumanInboxService(_context, service);
        (await inbox.ListAsync(_projectId)).Single().ClaimedByAlias.ShouldBe("Dan");

        var released = await service.UnclaimAsync(_projectId, "feat-a", HolderSessionId);

        released.ClaimSessionId.ShouldBeNull();
        var entry = (await inbox.ListAsync(_projectId)).Single();
        entry.Phase.ShouldBe(CeremonyPhasePlanner.AwaitingHumanLabel);
        entry.ClaimedByAlias.ShouldBeNull();
        entry.ClaimState.ShouldBeNull();
    }

    [Fact]
    public async Task SetStatus_CloseDone_ReleasesClaim()
    {
        await CreateFeatureAsync();
        var claimService = CreateClaimService();
        await claimService.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        feature.SetStatus(PlanningFeatureStatus.InProgress);
        await _context.SaveChangesAsync();
        await SeedResultWithTapAsync("feat-a");
        await CreateFeatureService().SetStatusAsync(
            _projectId, "feat-a", PlanningFeatureStatus.Done, "closed by user request",
            requireHumanTap: true, ct: default);

        var closed = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        closed.ClaimSessionId.ShouldBeNull();
        closed.ClaimedByAlias.ShouldBeNull();
        var log = PlanningLog.Parse(closed.Body).Select(l => l.Text).ToList();
        log.ShouldContain(t => t.StartsWith("done via agent review by Dan (qwen), approved by human tap "));
        log.Last().ShouldBe("claim released on done-close");
    }

    [Fact]
    public async Task Update_HumanDoorClose_ReleasesClaim()
    {
        await CreateFeatureAsync();
        await CreateClaimService()
            .ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        feature.SetStatus(PlanningFeatureStatus.InProgress);
        await _context.SaveChangesAsync();
        await SeedResultWithTapAsync("feat-a");

        await CreateFeatureService().UpdateAsync(
            _projectId, "feat-a", null, null, PlanningFeatureStatus.Done, default);

        var closed = await _context.PlanningFeatures.AsNoTracking()
            .SingleAsync(f => f.Slug == "feat-a");
        closed.ClaimSessionId.ShouldBeNull();
        PlanningLog.Parse(closed.Body).Last().Text.ShouldBe("claim released on done-close");
    }

    [Fact]
    public async Task Claim_TargetHeldByForeignAlive_InBusyLane_YieldsClaimHeldNotLaneSerial()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-a", "Bobby", "claude", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimHeld);
        ex.Message.ShouldContain("Dan by qwen (alive)");
    }

    [Fact]
    public async Task Claim_RestartAdoptOfOwnFeatureInBusyLane_SucceedsWithoutOverride()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20));
        var restartSessionId = Guid.NewGuid();

        var adopted = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", restartSessionId);

        adopted.ClaimSessionId.ShouldBe(restartSessionId);
        adopted.ClaimedByAlias.ShouldBe("Dan");
        PlanningLog.Parse(adopted.Body).Last().Text
            .ShouldBe("session restarted, claim adopted by Dan by qwen");
    }

    [Fact]
    public async Task Claim_CaseOnlyRosterRename_KeepsEnforcementOnTheLane()
    {
        var laneService = new LaneService(_context, CreateClaimService());
        _context.PlanningLanes.Add(PlanningLane.Create(_projectId, "frontend", null, null));
        await _context.SaveChangesAsync();
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await laneService.SetLanesAsync(_projectId, [new LaneUpsert("Frontend", null, null)]);
        await CreateFeatureService().UpdateAsync(
            _projectId, "feat-b", null, null, status: null, priority: null, lane: "Frontend", ct: default);
        await CreateClaimService().ClaimAsync(
            _projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var ex = await Should.ThrowAsync<PlanningException>(() => CreateClaimService()
            .ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.LaneClaimHeld);
        ex.Message.ShouldContain("feat-a");
    }

    [Fact]
    public async Task Claim_LaneBusyByAnotherAlias_ThrowsNamingLaneAndHolder()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.LaneClaimHeld);
        ex.Message.ShouldContain("frontend");
        ex.Message.ShouldContain("Dan by qwen");
        ex.Message.ShouldContain("feat-a");
    }

    [Fact]
    public async Task Claim_BoundLaneWithOpenFeaturesAndNoClaims_ClaimsCleanWithoutOverride()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        await CreateFeatureAsync("feat-c");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await SetLaneAsync("feat-c", "frontend");

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId);

        claimed.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Claim_LaneBusyWithOverride_Passes()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var claimed = await service.ClaimAsync(
            _projectId, "feat-b", "Bobby", "claude", ForeignSessionId, overrideLaneCheck: true);

        claimed.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Claim_LaneHoldsOnlyStaleClaimByAnotherAlias_ClaimsClean()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);
        await AgeHolderSessionAsync(DateTime.UtcNow.AddHours(-1));

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId);

        claimed.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Claim_CallerHoldsAliveClaimOnLaneMate_ClaimsCleanWithoutOverride()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "Dan", "qwen", ForeignSessionId);

        claimed.ClaimedByAlias.ShouldBe("Dan");
        claimed.ClaimedByVendor.ShouldBe("qwen");
    }

    [Fact]
    public async Task Claim_LaneMateClaimSameAliasDifferentVendor_IsForeignAndBlocks()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-b", "Dan", "claude", ForeignSessionId));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.LaneClaimHeld);
        ex.Message.ShouldContain("Dan by qwen");
        ex.Message.ShouldContain("feat-a");
    }

    [Fact]
    public async Task Claim_NoLaneSet_LaneChecksAreSkipped()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId);

        claimed.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Claim_ReClaimOfTheSameFeature_IsUnaffectedByLaneChecks()
    {
        await CreateFeatureAsync("feat-a");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var again = await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId);

        again.ClaimSessionId.ShouldBe(HolderSessionId);
    }

    [Fact]
    public async Task Claim_OtherLaneFeaturesAllDone_Passes()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        var finished = await _context.PlanningFeatures.SingleAsync(f => f.Slug == "feat-a");
        finished.SetStatus(PlanningFeatureStatus.Done);
        await _context.SaveChangesAsync();

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId);

        claimed.ClaimedByAlias.ShouldBe("Bobby");
    }

    [Fact]
    public async Task Claim_OwnLaneMateClaimReleased_ClaimsClean()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);
        await service.UnclaimAsync(_projectId, "feat-a", HolderSessionId);

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "Dan", "qwen", ForeignSessionId);

        claimed.ClaimedByAlias.ShouldBe("Dan");
    }

    [Fact]
    public async Task HumanClaim_IntoBusyLane_IsNeverLocked()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "frontend");
        await SetLaneAsync("feat-b", "frontend");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var claimed = await service.ClaimAsync(_projectId, "feat-b", "board", "human", null);

        claimed.ClaimedByHuman.ShouldBeTrue();
    }

    [Fact]
    public async Task Claim_UnknownLaneStillEnforcesChecksByName()
    {
        await CreateFeatureAsync("feat-a");
        await CreateFeatureAsync("feat-b");
        var service = CreateClaimService();
        await SetLaneAsync("feat-a", "ghost-lane");
        await SetLaneAsync("feat-b", "ghost-lane");
        await service.ClaimAsync(_projectId, "feat-a", "Dan", "qwen", HolderSessionId, overrideLaneCheck: true);

        var ex = await Should.ThrowAsync<PlanningException>(() => service
            .ClaimAsync(_projectId, "feat-b", "Bobby", "claude", ForeignSessionId));

        ex.Code.ShouldBe(PlanningFailureCode.LaneClaimHeld);
        ex.Message.ShouldContain("ghost-lane");
        ex.Message.ShouldContain("feat-a");
    }

    private async Task SetLaneAsync(string slug, string lane)
    {
        var feature = await _context.PlanningFeatures.SingleAsync(f => f.Slug == slug);
        feature.SetLane(lane);
        await _context.SaveChangesAsync();
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
