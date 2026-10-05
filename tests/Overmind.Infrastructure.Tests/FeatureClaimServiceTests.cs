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

    private async Task<PlanningFeature> CreateFeatureAsync(string slug = "feat-a") =>
        await CreateFeatureService().CreateAsync(_projectId, slug, "Title", "body");

    private async Task AgeHolderSessionAsync(DateTime lastSeenAt, DateTime? endedAt = null)
    {
        var session = _context.AgentSessions.Single(s => s.Id == HolderSessionId);
        _context.AgentSessions.Remove(session);
        _context.AgentSessions.Add(AgentSession.Restore(
            HolderSessionId, "Dan", "qwen", lastSeenAt.AddMinutes(-30), lastSeenAt, endedAt));
        await _context.SaveChangesAsync();
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
