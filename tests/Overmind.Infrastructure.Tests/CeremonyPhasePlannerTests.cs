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
            PlanningFeatureStatus.Draft, null, null, 0).ShouldBe("drafting");
    }

    [Fact]
    public void SpecDraft_IsWritingSpec()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Draft, null, 0).ShouldBe("writing spec");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(PlanningArtifactStatus.Draft)]
    public void SpecValidated_PlanMissingOrDraft_IsPlanning(PlanningArtifactStatus? planStatus)
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, planStatus, 0).ShouldBe("planning");
    }

    [Fact]
    public void SpecAndPlanValidated_TasksNotStarted_IsPlanValidated()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 0)
            .ShouldBe("plan validated");
    }

    [Fact]
    public void ValidatedButStillDraft_FollowsTheMachine_NotTheLifecycle()
    {
        var label = CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Draft,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 3);

        label.ShouldBe("plan validated");
        label.ShouldNotBe("in progress");
    }

    [Fact]
    public void InProgress_WithTasksInFlight_IsInProgress()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.InProgress,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 3)
            .ShouldBe("in progress");
    }

    [Fact]
    public void InProgress_WithoutTasks_StaysPlanValidated()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.InProgress,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 0)
            .ShouldBe("plan validated");
    }

    [Fact]
    public void Review_IsReview()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Review,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 2)
            .ShouldBe("review");
    }

    [Fact]
    public void Done_IsDone_EvenWithSparseArtifacts()
    {
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Done, null, null, 0).ShouldBe("done");
        CeremonyPhasePlanner.DeriveLabel(
            PlanningFeatureStatus.Done,
            PlanningArtifactStatus.Validated, PlanningArtifactStatus.Validated, 5)
            .ShouldBe("done");
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
