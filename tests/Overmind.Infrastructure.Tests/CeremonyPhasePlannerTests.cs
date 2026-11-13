using Overmind.Core;
using System.Globalization;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public class CeremonyPhasePlannerTests
{
    private const string GoalBody = """
        # feature

        <goal>
        Crash on save when the workspace file is read-only.
        </goal>
        """;

    [Fact]
    public void Draft_WithoutSpecArtifact_IsDrafting()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft, null, null, 0, 0, null).ShouldBe("drafting");
    }

    [Fact]
    public void SpecDraft_IsWritingSpec()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Draft, null, 0, 0, null).ShouldBe("writing spec");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PlanningArtifactStatus.Draft)]
    public void SpecValidated_PlanMissingOrDraft_IsPlanning(PlanningArtifactStatus? planStatus)
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, planStatus, 0, 0, null).ShouldBe("planning");
    }

    [Fact]
    public void SpecAndPlanValidated_TasksNotStarted_IsPlanValidated()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 0, 0, null)
            .ShouldBe("plan validated");
    }

    [Fact]
    public void ValidatedButStillDraft_FollowsTheMachine_NotTheLifecycle()
    {
        var label = CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 3, 3, null);

        label.ShouldBe("plan validated");
        label.ShouldNotBe("in progress");
    }

    [Fact]
    public void InProgress_WithTasksInFlight_IsInProgress()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.InProgress,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 3, 1, null)
            .ShouldBe("in progress");
    }

    [Fact]
    public void InProgress_WithoutTasks_StaysPlanValidated()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.InProgress,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 0, 0, null)
            .ShouldBe("plan validated");
    }

    [Fact]
    public void Review_IsReview()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Review,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 2, 2, null)
            .ShouldBe("review");
    }

    [Fact]
    public void Done_IsDone_EvenWithSparseArtifacts()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Done, null, null, 0, 0, null).ShouldBe("done");
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Done,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 5, 5,
            PlanningArtifactStatus.Validated)
            .ShouldBe("done");
    }

    public static IEnumerable<object[]> LabelMatrix()
    {
        PlanningFeatureStatus[] statuses =
        [
            PlanningFeatureStatus.Draft,
            PlanningFeatureStatus.Ready,
            PlanningFeatureStatus.InProgress,
            PlanningFeatureStatus.Done,
            PlanningFeatureStatus.Review,
        ];
        PlanningArtifactStatus?[] documentStatuses = [null, PlanningArtifactStatus.Draft, PlanningArtifactStatus.Validated];
        int[] taskTotals = [0, 3];
        PlanningArtifactStatus?[] resultStatuses = [null, PlanningArtifactStatus.Draft, PlanningArtifactStatus.Validated];

        foreach (var status in statuses)
        {
            foreach (var specStatus in documentStatuses)
            {
                foreach (var planStatus in documentStatuses)
                {
                    foreach (var taskTotal in taskTotals)
                    {
                        foreach (var resultStatus in resultStatuses)
                        {
                            yield return [status, specStatus, planStatus, taskTotal, resultStatus];
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(LabelMatrix))]
    public void LabelMatrix_PinsEveryCombination(
        PlanningFeatureStatus status,
        PlanningArtifactStatus? specStatus,
        PlanningArtifactStatus? planStatus,
        int taskTotal,
        PlanningArtifactStatus? resultStatus)
    {
        var label = CeremonyPhasePlanner.DeriveLabel(
            status, specStatus, planStatus, taskTotal, taskDone: taskTotal, resultStatus);

        label.ShouldBe(PinnedLabel(status, specStatus, planStatus, taskTotal, resultStatus));
    }

    private static string PinnedLabel(
        PlanningFeatureStatus status,
        PlanningArtifactStatus? specStatus,
        PlanningArtifactStatus? planStatus,
        int taskTotal,
        PlanningArtifactStatus? resultStatus)
    {
        if (status == PlanningFeatureStatus.Done)
        {
            return "done";
        }

        if (resultStatus == PlanningArtifactStatus.Validated &&
            (status == PlanningFeatureStatus.Review ||
             (status == PlanningFeatureStatus.InProgress && taskTotal > 0)))
        {
            return "awaiting-human";
        }

        if (status == PlanningFeatureStatus.Review)
        {
            return "review";
        }

        if (status == PlanningFeatureStatus.InProgress && taskTotal > 0)
        {
            return "in progress";
        }

        if (specStatus is null)
        {
            return "drafting";
        }

        if (specStatus == PlanningArtifactStatus.Draft)
        {
            return "writing spec";
        }

        if (specStatus == PlanningArtifactStatus.Validated && planStatus != PlanningArtifactStatus.Validated)
        {
            return "planning";
        }

        return "plan validated";
    }

    [Fact]
    public void InProgress_AllTasksDone_WithValidatedResult_IsAwaitingHuman()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.InProgress,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 5, 5,
            PlanningArtifactStatus.Validated)
            .ShouldBe("awaiting-human");
    }

    [Fact]
    public void InProgress_WithOpenTasks_AndValidatedResult_StaysInProgress()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.InProgress,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 5, 3,
            PlanningArtifactStatus.Validated)
            .ShouldBe("in progress");
    }

    [Fact]
    public void Review_WithValidatedResult_IsAwaitingHuman()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Review,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 2, 2,
            PlanningArtifactStatus.Validated)
            .ShouldBe("awaiting-human");
    }

    [Fact]
    public void Review_ResultMissingOrUnvalidated_StaysReview()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Review,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 2, 2, null)
            .ShouldBe("review");
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Review,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 2, 2,
            PlanningArtifactStatus.Draft)
            .ShouldBe("review");
    }

    [Fact]
    public void DraftOrReady_WithValidatedResult_KeepsCeremonyLabel()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 3, 3,
            PlanningArtifactStatus.Validated)
            .ShouldBe("plan validated");
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Ready,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 3, 3,
            PlanningArtifactStatus.Validated)
            .ShouldBe("plan validated");
    }

    [Fact]
    public void Review_ZeroTasks_WithValidatedResult_IsAwaitingHuman()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Review, null, null, 0, 0,
            PlanningArtifactStatus.Validated)
            .ShouldBe("awaiting-human");
    }

    [Fact]
    public void BriefExcerpt_FirstGoalParagraph_CappedAt500()
    {
        var body = $"""
            # feature

            <goal>
            {new string('а', 510)}
            </goal>
            """;

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldNotBeNull();
        excerpt.Length.ShouldBe(501);
        excerpt.ShouldEndWith("…");
        excerpt.ShouldStartWith(new string('а', 500));
    }

    [Fact]
    public void BriefExcerpt_ExactlyAtCap_ReturnedVerbatim_NoEllipsis()
    {
        var paragraph = new string('а', CeremonyPhasePlanner.ExcerptCap);
        var body = $"<goal>\n{paragraph}\n</goal>";

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldBe(paragraph);
        excerpt.ShouldNotEndWith("…");
    }

    [Fact]
    public void BriefExcerpt_ShortGoal_ReturnedVerbatim_NoEllipsis()
    {
        var body = """
            <goal>
            Crash on save when the workspace file is read-only.
            </goal>
            """;

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldBe("Crash on save when the workspace file is read-only.");
    }

    [Fact]
    public void BriefExcerpt_MultilineParagraph_LinesJoined()
    {
        var body = """
            <goal>
            Crash on save
            when the workspace file is read-only.
            </goal>
            """;

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldBe("Crash on save when the workspace file is read-only.");
    }

    [Fact]
    public void BriefExcerpt_FirstParagraphWins_SecondIgnored()
    {
        var body = """
            <goal>
            First paragraph is the pitch.

            Second paragraph with details nobody reads on a card.
            </goal>
            """;

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldStartWith("First paragraph is the pitch.");
        excerpt.ShouldNotContain("Second paragraph");
    }

    [Fact]
    public void BriefExcerpt_NoGoal_ReturnsNull()
    {
        CeremonyPhasePlanner.BriefExcerpt("# feature without a goal section").ShouldBeNull();
    }

    [Fact]
    public void BriefExcerpt_NullBody_ReturnsNull()
    {
        CeremonyPhasePlanner.BriefExcerpt(null).ShouldBeNull();
    }

    [Fact]
    public void BriefExcerpt_EmptyGoal_ReturnsNull_NotEmptyString()
    {
        CeremonyPhasePlanner.BriefExcerpt("<goal>\n</goal>").ShouldBeNull();
        CeremonyPhasePlanner.BriefExcerpt("<goal>\n   \n</goal>").ShouldBeNull();
    }

    [Fact]
    public void BriefExcerpt_CjkAtCapBoundary_DoesNotSplitSurrogatePairs()
    {
        var cjk = new string('写', 500) + "🗿";
        var body = $"<goal>\n{cjk}\n</goal>";

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldNotBeNull();
        excerpt.ShouldEndWith("…");
        excerpt.ShouldNotContain("\uD83D"); // high surrogate never travels alone
        new StringInfo(excerpt).LengthInTextElements.ShouldBe(501); // 500 ideographs + ellipsis
    }

    [Fact]
    public void BriefExcerpt_CyrillicOverCap_TruncatesAtCap()
    {
        var body = $"<goal>\n{new string('ж', 550)}\n</goal>";

        var excerpt = CeremonyPhasePlanner.BriefExcerpt(body);

        excerpt.ShouldNotBeNull();
        excerpt.Length.ShouldBe(501);
        excerpt.EndsWith("…").ShouldBeTrue();
        excerpt.TrimEnd('…').Length.ShouldBe(500);
    }
}
