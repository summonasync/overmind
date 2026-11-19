using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class PlanningFeatureTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public void Create_SetsDefaults()
    {
        var before = DateTime.UtcNow;

        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth flow", "body text");

        feature.Id.ShouldNotBe(Guid.Empty);
        feature.ProjectId.ShouldBe(ProjectId);
        feature.Slug.ShouldBe("001-auth");
        feature.Title.ShouldBe("Auth flow");
        feature.Body.ShouldBe("body text");
        feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
        feature.Priority.ShouldBe(FeaturePriority.P2);
        feature.Version.ShouldBe(1);
        feature.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
        feature.UpdatedAt.ShouldBe(feature.CreatedAt);
        feature.Artifacts.ShouldBeEmpty();
    }

    [Fact]
    public void Create_NullBody_StoredAsEmpty()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", null!);

        feature.Body.ShouldBe(string.Empty);
    }

    [Fact]
    public void Create_ExplicitPriority_Stored()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body", FeaturePriority.P1);

        feature.Priority.ShouldBe(FeaturePriority.P1);
    }

    [Fact]
    public void SetPriority_ChangesPriorityTouchesUpdatedAtKeepsVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var before = feature.UpdatedAt;

        feature.SetPriority(FeaturePriority.P0);

        feature.Priority.ShouldBe(FeaturePriority.P0);
        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
        feature.Version.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptySlug_Throws(string slug)
    {
        Should.Throw<ArgumentException>(() =>
            PlanningFeature.Create(ProjectId, slug, "title", "body"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyTitle_Throws(string title)
    {
        Should.Throw<ArgumentException>(() =>
            PlanningFeature.Create(ProjectId, "slug", title, "body"));
    }

    [Fact]
    public void Update_InDraft_DoesNotBumpVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var createdAt = feature.CreatedAt;

        feature.Update("new title", "new body");

        feature.Title.ShouldBe("new title");
        feature.Body.ShouldBe("new body");
        feature.Version.ShouldBe(1);
        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(createdAt);
    }

    [Fact]
    public void Update_AfterLeavingDraft_BumpsVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetStatus(PlanningFeatureStatus.Ready);

        feature.Update("new title", "new body");

        feature.Version.ShouldBe(2);
    }

    [Fact]
    public void Update_AlwaysTouchesUpdatedAt()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var before = feature.UpdatedAt;

        feature.Update("changed", "body");

        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void SetStatus_ChangesStatus()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        feature.SetStatus(PlanningFeatureStatus.InProgress);

        feature.Status.ShouldBe(PlanningFeatureStatus.InProgress);
    }

    [Fact]
    public void SetStatus_WithNoteAndSource_AppendsSourcedLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        feature.SetStatus(PlanningFeatureStatus.Ready, "by user request in chat", PlanningLogSource.Agent);

        feature.Status.ShouldBe(PlanningFeatureStatus.Ready);
        var lines = PlanningLog.Parse(feature.Body);
        lines.ShouldHaveSingleItem();
        lines[0].Text.ShouldBe("by user request in chat");
        lines[0].Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public void Update_NullTitle_NullBody_ChangesNothingButTouchesUpdatedAt()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var originalTitle = feature.Title;
        var originalBody = feature.Body;
        var originalVersion = feature.Version;

        feature.Update(null, null);

        feature.Title.ShouldBe(originalTitle);
        feature.Body.ShouldBe(originalBody);
        feature.Version.ShouldBe(originalVersion);
    }

    [Fact]
    public void Update_TitleOnly_KeepsBody()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "original body");

        feature.Update("new title", null);

        feature.Title.ShouldBe("new title");
        feature.Body.ShouldBe("original body");
    }

    [Fact]
    public void Update_BodyStoredVerbatim_PreservesWhitespace()
    {
        var verbatimBody = "  leading\ntrailing\n\n";
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "initial");

        feature.Update(null, verbatimBody);

        feature.Body.ShouldBe(verbatimBody);
    }

    [Fact]
    public void Restore_RoundTripsAllFields()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var feature = PlanningFeature.Restore(
            ProjectId,
            "001-auth",
            "Auth flow",
            "imported body",
            PlanningFeatureStatus.Ready,
            version: 3,
            createdAt: createdAt,
            updatedAt: updatedAt);

        feature.ProjectId.ShouldBe(ProjectId);
        feature.Slug.ShouldBe("001-auth");
        feature.Title.ShouldBe("Auth flow");
        feature.Body.ShouldBe("imported body");
        feature.Status.ShouldBe(PlanningFeatureStatus.Ready);
        feature.ClaimSessionId.ShouldBeNull();
        feature.Priority.ShouldBe(FeaturePriority.P2);
        feature.Version.ShouldBe(3);
        feature.CreatedAt.ShouldBe(createdAt);
        feature.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void Claim_SetsClaimFieldsAndTouchesUpdatedAt()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        var before = feature.UpdatedAt;
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        feature.Claim("Dan", "qwen", sessionId, now);

        feature.ClaimedByAlias.ShouldBe("Dan");
        feature.ClaimedByVendor.ShouldBe("qwen");
        feature.ClaimSessionId.ShouldBe(sessionId);
        feature.ClaimedAt.ShouldBe(now);
        feature.Status.ShouldBe(PlanningFeatureStatus.Draft);
        feature.UpdatedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void Claim_DoneFeature_Throws()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetStatus(PlanningFeatureStatus.Done);

        var ex = Should.Throw<PlanningException>(() =>
            feature.Claim("Dan", "qwen", Guid.NewGuid(), DateTime.UtcNow));

        ex.Kind.ShouldBe(PlanningFailureKind.RuleViolation);
        ex.Code.ShouldBe(PlanningFailureCode.FeatureClaimRefused);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Claim_EmptyAlias_Throws(string? alias)
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        Should.Throw<ArgumentException>(() =>
            feature.Claim(alias!, "qwen", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Claim_IdentityOverCap_Throws()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        Should.Throw<ArgumentException>(() =>
            feature.Claim(new string('a', 33), "qwen", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Claim_HumanClaim_MarksAndReleaseClearsTheFlag()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.ClaimedByHuman.ShouldBeFalse();

        feature.Claim("Dana", "human", Guid.NewGuid(), DateTime.UtcNow, byHuman: true);
        feature.ClaimedByHuman.ShouldBeTrue();

        feature.Release();
        feature.ClaimedByHuman.ShouldBeFalse();
    }

    [Fact]
    public void Release_ClearsClaimFields()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.Claim("Dan", "qwen", Guid.NewGuid(), DateTime.UtcNow);

        feature.Release();

        feature.ClaimedByAlias.ShouldBeNull();
        feature.ClaimedByVendor.ShouldBeNull();
        feature.ClaimedAt.ShouldBeNull();
        feature.ClaimSessionId.ShouldBeNull();
    }

    [Fact]
    public void Reattribute_HeldClaim_MovesOnlyThePair()
    {
        var sessionId = Guid.NewGuid();
        var claimedAt = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.Claim("agent", "unknown", sessionId, claimedAt);

        var moved = feature.Reattribute("mark-wolff", "claude");

        moved.ShouldBeTrue();
        feature.ClaimedByAlias.ShouldBe("mark-wolff");
        feature.ClaimedByVendor.ShouldBe("claude");
        feature.ClaimSessionId.ShouldBe(sessionId);
        feature.ClaimedAt.ShouldBe(claimedAt);
    }

    [Theory]
    [InlineData("agent", "unknown", false)]
    [InlineData("agent", "claude", true)]
    [InlineData("mark-wolff", "unknown", true)]
    public void Reattribute_PairAlreadyInForce_IsNoOp(string alias, string vendor, bool expected)
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.Claim("agent", "unknown", Guid.NewGuid(), DateTime.UtcNow);

        feature.Reattribute(alias, vendor).ShouldBe(expected);
    }

    [Fact]
    public void Reattribute_UnclaimedFeature_IsNoOp()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        feature.Reattribute("mark-wolff", "claude").ShouldBeFalse();
        feature.ClaimedByAlias.ShouldBeNull();
        feature.ClaimedByVendor.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "claude")]
    [InlineData("mark-wolff", " ")]
    [InlineData("mark-wolff", null)]
    public void Reattribute_EmptyIdentity_Throws(string? alias, string? vendor)
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.Claim("agent", "unknown", Guid.NewGuid(), DateTime.UtcNow);

        Should.Throw<ArgumentException>(() => feature.Reattribute(alias!, vendor!));
    }

    [Fact]
    public void Reattribute_IdentityOverCap_Throws()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.Claim("agent", "unknown", Guid.NewGuid(), DateTime.UtcNow);

        Should.Throw<ArgumentException>(() =>
            feature.Reattribute(new string('a', 33), "claude"));

        feature.ClaimedByAlias.ShouldBe("agent");
    }

    [Fact]
    public void AppendLog_AppendsLineWithoutBumpingVersion()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetStatus(PlanningFeatureStatus.Ready);

        feature.AppendLog("claim recorded", PlanningLogSource.Agent);

        var lines = PlanningLog.Parse(feature.Body);
        lines.ShouldHaveSingleItem();
        lines[0].Text.ShouldBe("claim recorded");
        lines[0].Source.ShouldBe(PlanningLogSource.Agent);
        feature.Version.ShouldBe(1);
    }

    [Fact]
    public void AddBlockedBy_SameProjectFeature_StoresSingleLink()
    {
        var blocker = PlanningFeature.Create(ProjectId, "001-auth", "Auth", "body");
        var feature = PlanningFeature.Create(ProjectId, "002-api", "Api", "body");

        feature.AddBlockedBy(blocker);
        feature.AddBlockedBy(blocker);

        var link = feature.BlockedBy.Single();
        link.FeatureId.ShouldBe(feature.Id);
        link.BlockedByFeatureId.ShouldBe(blocker.Id);
    }

    [Fact]
    public void AddBlockedBy_SelfReference_Throws()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", "body");

        Should.Throw<ArgumentException>(() => feature.AddBlockedBy(feature));
    }

    [Fact]
    public void AddBlockedBy_FeatureFromAnotherProject_Throws()
    {
        var blocker = PlanningFeature.Create(Guid.NewGuid(), "001-auth", "Auth", "body");
        var feature = PlanningFeature.Create(ProjectId, "002-api", "Api", "body");

        Should.Throw<ArgumentException>(() => feature.AddBlockedBy(blocker));
    }

    [Fact]
    public void RemoveBlockedBy_ExistingLink_RemovesIt()
    {
        var blocker = PlanningFeature.Create(ProjectId, "001-auth", "Auth", "body");
        var feature = PlanningFeature.Create(ProjectId, "002-api", "Api", "body");
        feature.AddBlockedBy(blocker);

        feature.RemoveBlockedBy(blocker);

        feature.BlockedBy.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveBlockedBy_UnknownBlocker_IsNoOp()
    {
        var stranger = PlanningFeature.Create(ProjectId, "001-auth", "Auth", "body");
        var feature = PlanningFeature.Create(ProjectId, "002-api", "Api", "body");

        feature.RemoveBlockedBy(stranger);

        feature.BlockedBy.ShouldBeEmpty();
    }

    [Fact]
    public void GetBlocks_StoresOneDirection_DerivesReverseInMemory()
    {
        var first = PlanningFeature.Create(ProjectId, "001-auth", "Auth", "body");
        var second = PlanningFeature.Create(ProjectId, "002-api", "Api", "body");
        var third = PlanningFeature.Create(ProjectId, "003-ui", "Ui", "body");
        second.AddBlockedBy(first);
        third.AddBlockedBy(first);
        third.AddBlockedBy(second);

        var projectFeatures = new[] { first, second, third };

        first.GetBlocks(projectFeatures).Select(f => f.Slug).ShouldBe(["002-api", "003-ui"]);
        second.GetBlocks(projectFeatures).Select(f => f.Slug).ShouldBe(["003-ui"]);
        third.GetBlocks(projectFeatures).ShouldBeEmpty();
    }

    private const string LoggedBody = """
        # 001-auth

        <log>
        - 2026-09-01 — seeded
        - 2026-09-02 [agent] — review feedback applied
        </log>
        """;

    [Fact]
    public void Update_MutatedStoredLogLine_ThrowsAndLeavesFeatureUntouched()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", LoggedBody);
        var originalTitle = feature.Title;
        var originalBody = feature.Body;
        var originalVersion = feature.Version;
        var originalUpdatedAt = feature.UpdatedAt;

        var ex = Should.Throw<PlanningException>(() => feature.Update(null, """
            # 001-auth

            <log>
            - 2026-09-01 — seeded
            - 2026-09-02 [agent] — history was polished
            </log>
            """));

        ex.Code.ShouldBe(PlanningFailureCode.LogHistoryNotAppendable);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("log line 1");
        ex.Message.ShouldContain("stored 2");
        ex.Message.ShouldContain("incoming 2");
        feature.Title.ShouldBe(originalTitle);
        feature.Body.ShouldBe(originalBody);
        feature.Version.ShouldBe(originalVersion);
        feature.UpdatedAt.ShouldBe(originalUpdatedAt);
    }

    [Fact]
    public void Update_DeletedStoredLogTail_Throws()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", LoggedBody);

        var ex = Should.Throw<PlanningException>(() => feature.Update(null, """
            # 001-auth

            <log>
            - 2026-09-01 — seeded
            </log>
            """));

        ex.Code.ShouldBe(PlanningFailureCode.LogHistoryNotAppendable);
        ex.Message.ShouldContain("log line 1");
        ex.Message.ShouldContain("stored 2");
        ex.Message.ShouldContain("incoming 1");
    }

    [Fact]
    public void Update_PureAppendWithSourcedNote_LandsOneDatedSourcedLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", LoggedBody);

        feature.Update(null, LoggedBody, "task 083/01 executed", PlanningLogSource.Agent);

        var lines = PlanningLog.Parse(feature.Body);
        lines.Count.ShouldBe(3);
        lines[2].Text.ShouldBe("task 083/01 executed");
        lines[2].Source.ShouldBe(PlanningLogSource.Agent);
        lines[2].Date.ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public void Update_EmptyStoredLog_AcceptsAnyIncomingBody()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", "plain body, no log yet");

        feature.Update(null, """
            # 001-auth

            <log>
            - 2020-01-01 — brand new history
            </log>
            """);

        feature.Body.ShouldContain("brand new history");
        PlanningLog.Parse(feature.Body).ShouldHaveSingleItem();
    }

    [Fact]
    public void Update_TitleOnlyWithIntactLog_Passes()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", LoggedBody);

        feature.Update("Auth flow v2", null);

        feature.Title.ShouldBe("Auth flow v2");
        feature.Body.ShouldBe(LoggedBody);
        PlanningLog.Parse(feature.Body).Count.ShouldBe(2);
    }

    [Fact]
    public void Update_TitleOnlyWithSourcedNote_AppendsToExistingLog()
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", LoggedBody);

        feature.Update("Auth flow v2", null, "renamed by user request", PlanningLogSource.Ui);

        var lines = PlanningLog.Parse(feature.Body);
        lines.Count.ShouldBe(3);
        lines[2].Text.ShouldBe("renamed by user request");
        lines[2].Source.ShouldBe(PlanningLogSource.Ui);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Update_AbsentLogNote_AppendsNothingAndStillGuards(string? logNote)
    {
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth", LoggedBody);

        feature.Update("renamed", LoggedBody, logNote);

        PlanningLog.Parse(feature.Body).Count.ShouldBe(2);
        feature.Title.ShouldBe("renamed");

        Should.Throw<PlanningException>(() =>
            feature.Update(null, "no log section anymore", logNote))
            .Code.ShouldBe(PlanningFailureCode.LogHistoryNotAppendable);
    }

    [Fact]
    public void Update_VersionPinnedToBumpOnlyOutsideDraft()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        feature.Update("t2", "b2");
        feature.Version.ShouldBe(1);

        feature.SetStatus(PlanningFeatureStatus.Ready);
        feature.Version.ShouldBe(1);

        feature.Update("t3", "b3");
        feature.Version.ShouldBe(2);

        feature.Update("t4", "b4");
        feature.Version.ShouldBe(3);
    }

    [Fact]
    public void SetCodePin_BranchAndWorktree_SetValuesAndAppendOneLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        var changed = feature.SetCodePin("feat/pin-branch", "/repo/wt", PlanningLogSource.Agent);

        changed.ShouldBeTrue();
        feature.PinnedBranch.ShouldBe("feat/pin-branch");
        feature.PinnedWorktreePath.ShouldBe("/repo/wt");
        feature.Version.ShouldBe(1);

        var lines = PlanningLog.Parse(feature.Body);
        lines.ShouldHaveSingleItem();
        lines[0].Text.ShouldBe("code pin set: branch 'feat/pin-branch', worktree '/repo/wt'");
        lines[0].Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SetCodePin_BlankValues_NormalizeToNull(string? blank)
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        feature.SetCodePin(blank, "  ", PlanningLogSource.Ui);

        feature.PinnedBranch.ShouldBeNull();
        feature.PinnedWorktreePath.ShouldBeNull();
        PlanningLog.Parse(feature.Body).ShouldBeEmpty();
    }

    [Fact]
    public void SetCodePin_Replace_UpdatesValuesAndAppendsOneLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("old-branch", null, PlanningLogSource.Agent);

        feature.SetCodePin("new-branch", "/repo/wt", PlanningLogSource.Agent);

        feature.PinnedBranch.ShouldBe("new-branch");
        feature.PinnedWorktreePath.ShouldBe("/repo/wt");
        var lines = PlanningLog.Parse(feature.Body);
        lines.Count.ShouldBe(2);
        lines[1].Text.ShouldBe("code pin set: branch 'new-branch', worktree '/repo/wt'");
    }

    [Fact]
    public void SetCodePin_IdenticalValues_NoOpWithoutLogLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("feat/pin", "/repo/wt", PlanningLogSource.Agent);

        var changed = feature.SetCodePin("feat/pin", " /repo/wt ", PlanningLogSource.Agent);

        changed.ShouldBeFalse();
        PlanningLog.Parse(feature.Body).ShouldHaveSingleItem();
    }

    [Fact]
    public void SetCodePin_ClearOnEmptyPin_NoOpWithoutLogLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");

        var changed = feature.SetCodePin(null, null, PlanningLogSource.Ui);

        changed.ShouldBeFalse();
        feature.PinnedBranch.ShouldBeNull();
        feature.PinnedWorktreePath.ShouldBeNull();
        PlanningLog.Parse(feature.Body).ShouldBeEmpty();
    }

    [Fact]
    public void SetCodePin_ClearPinnedPin_ClearsValuesAndAppendsOneLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("feat/pin", "/repo/wt", PlanningLogSource.Agent);

        var changed = feature.SetCodePin(null, "", PlanningLogSource.Agent);

        changed.ShouldBeTrue();
        feature.PinnedBranch.ShouldBeNull();
        feature.PinnedWorktreePath.ShouldBeNull();
        var lines = PlanningLog.Parse(feature.Body);
        lines.Count.ShouldBe(2);
        lines[1].Text.ShouldBe("code pin cleared");
    }

    [Fact]
    public void RemapCodePin_MovedProjectRoot_RewritesThePinWithOneOldToNewLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("feat/pin", "/old/.lanes/008", PlanningLogSource.Agent);

        var changed = feature.RemapCodePin("/old/.lanes/008", "/new/.lanes/008", PlanningLogSource.Ui);

        changed.ShouldBeTrue();
        feature.PinnedWorktreePath.ShouldBe("/new/.lanes/008");
        feature.PinnedBranch.ShouldBe("feat/pin");
        var moved = MovedPinLines(feature);
        moved.ShouldHaveSingleItem();
        moved[0].Text.ShouldBe("code pin moved with project root: '/old/.lanes/008' → '/new/.lanes/008'");
        moved[0].Source.ShouldBe(PlanningLogSource.Ui);
    }

    [Fact]
    public void RemapCodePin_ReplayedAfterTheFirstMove_ChangesNothingAndAddsNoSecondLine()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("feat/pin", "/old/.lanes/008", PlanningLogSource.Agent);
        feature.RemapCodePin("/old/.lanes/008", "/new/.lanes/008", PlanningLogSource.Ui);

        var again = feature.RemapCodePin("/old/.lanes/008", "/new/.lanes/008", PlanningLogSource.Ui);

        again.ShouldBeFalse();
        feature.PinnedWorktreePath.ShouldBe("/new/.lanes/008");
        MovedPinLines(feature).ShouldHaveSingleItem();
    }

    [Fact]
    public void RemapCodePin_WhenThePinIsNotWhatTheCallerRead_LeavesThePinAndLogsNothing()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("feat/pin", "/other/.lanes/008", PlanningLogSource.Agent);

        var changed = feature.RemapCodePin("/old/.lanes/008", "/new/.lanes/008", PlanningLogSource.Ui);

        changed.ShouldBeFalse();
        feature.PinnedWorktreePath.ShouldBe("/other/.lanes/008");
        MovedPinLines(feature).ShouldBeEmpty();
    }

    private static List<PlanningLogLine> MovedPinLines(PlanningFeature feature) =>
    [
        .. PlanningLog.Parse(feature.Body)
            .Where(line => line.Text.Contains("code pin moved", StringComparison.Ordinal))
    ];

    [Fact]
    public void SetCodePin_PartialClear_KeepsOtherHalf()
    {
        var feature = PlanningFeature.Create(ProjectId, "slug", "title", "body");
        feature.SetCodePin("feat/pin", "/repo/wt", PlanningLogSource.Agent);

        feature.SetCodePin(null, "/repo/wt", PlanningLogSource.Agent);

        feature.PinnedBranch.ShouldBeNull();
        feature.PinnedWorktreePath.ShouldBe("/repo/wt");
        PlanningLog.Parse(feature.Body).Last().Text.ShouldBe("code pin set: worktree '/repo/wt'");
    }

    [Fact]
    public void PatchSection_InsertWithInlineSection_RefusesInsteadOfAppendingADuplicate()
    {
        var body = "<goal>Ship the parser.</goal>\n";
        var feature = PlanningFeature.Create(ProjectId, "001-auth", "Auth flow", body);

        var ex = Should.Throw<PlanningException>(() =>
            feature.PatchSection("goal", "Revised goal.", remove: false, reason: null, "patch probe", PlanningLogSource.Agent));

        ex.Code.ShouldBe(PlanningFailureCode.PatchSectionNotFound);
        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Message.ShouldContain("A one-line '<goal>…</goal>' section exists");
        ex.Message.ShouldContain("re-send the body once");
        feature.Body.ShouldBe(body);
        feature.Version.ShouldBe(1);
        PlanningLog.Parse(feature.Body).ShouldBeEmpty();
    }
}
