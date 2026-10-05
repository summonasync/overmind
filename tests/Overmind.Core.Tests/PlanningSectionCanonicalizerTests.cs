using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

public class PlanningSectionCanonicalizerTests
{
    [Fact]
    public void Canonicalize_InlineSectionBetweenAnchoredOnes_ExpandsWrapperKeepsContent()
    {
        var body = "<goal>\nShip.\n</goal>\n\n<anti-scope>Parser stays anchored.</anti-scope>\n";

        var canonical = PlanningSectionCanonicalizer.Canonicalize(body);

        canonical.ShouldBe("<goal>\nShip.\n</goal>\n\n<anti-scope>\nParser stays anchored.\n</anti-scope>\n");
        PlanningSections.Find("anti-scope", canonical)!.Content.ShouldBe("Parser stays anchored.");
    }

    [Fact]
    public void Canonicalize_FrictionPayload_ExpandsInlineAdditionsOnly()
    {
        var body = """
            # Task 01

            <goal>
            Every section in a body becomes patchable.
            </goal>

            <scope>
            - canonicalize at the entrances
            </scope>

            <anti-scope>Parser stays anchored.</anti-scope>

            <constraints>No new packages.</constraints>
            """;
        var expected = """
            # Task 01

            <goal>
            Every section in a body becomes patchable.
            </goal>

            <scope>
            - canonicalize at the entrances
            </scope>

            <anti-scope>
            Parser stays anchored.
            </anti-scope>

            <constraints>
            No new packages.
            </constraints>
            """;

        var canonical = PlanningSectionCanonicalizer.Canonicalize(body);

        canonical.ShouldBe(expected);
        PlanningSections.List(canonical).ShouldBe(new[] { "goal", "scope", "anti-scope", "constraints" });
    }

    [Fact]
    public void Canonicalize_EmptyInlineSection_BecomesBareWrapperPair()
    {
        var canonical = PlanningSectionCanonicalizer.Canonicalize("<obstacles></obstacles>");

        canonical.ShouldBe("<obstacles>\n</obstacles>");
        PlanningSections.Find("obstacles", canonical)!.Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Canonicalize_WhitespaceOnlyInlineSection_BecomesBareWrapperPair()
    {
        PlanningSectionCanonicalizer.Canonicalize("<obstacles>   </obstacles>")
            .ShouldBe("<obstacles>\n</obstacles>");
    }

    [Fact]
    public void Canonicalize_CanonicalBody_IsByteIdentical()
    {
        var canonical = """
            # Task 01

            <goal>
            Every section in a body becomes patchable.
            </goal>

            <anti-scope>
            Parser stays anchored.
            </anti-scope>

            <log>
            - 2026-10-04 [agent] — seeded
            </log>
            """;

        PlanningSectionCanonicalizer.Canonicalize(canonical).ShouldBe(canonical);
    }

    [Fact]
    public void Canonicalize_SecondPass_IsByteIdentical()
    {
        var once = PlanningSectionCanonicalizer.Canonicalize(
            "<goal>Ship.</goal>\n\n<anti-scope>None.</anti-scope>\n");
        var twice = PlanningSectionCanonicalizer.Canonicalize(once);

        twice.ShouldBe(once);
    }

    [Fact]
    public void Canonicalize_NestedOneLineSections_FullyExpandInOnePass()
    {
        var once = PlanningSectionCanonicalizer.Canonicalize("<goal><scope>inner</scope></goal>");

        once.ShouldBe("<goal>\n<scope>\ninner\n</scope>\n</goal>");
        PlanningSectionCanonicalizer.Canonicalize(once).ShouldBe(once);
    }

    [Fact]
    public void Canonicalize_FencedExample_IsUntouched()
    {
        var body = "# Example\n\n```\n<goal>not a section</goal>\n```\n\n<scope>\nreal\n</scope>\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void Canonicalize_TildeFencedExample_IsUntouched()
    {
        var body = "~~~\n<goal>not a section</goal>\n~~~\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void Canonicalize_InlineLogWrapper_LogLineSurvivesVerbatim()
    {
        var canonical = PlanningSectionCanonicalizer.Canonicalize(
            "<log>- 2026-10-04 [agent] — seeded</log>");

        canonical.ShouldBe("<log>\n- 2026-10-04 [agent] — seeded\n</log>");
        var line = PlanningLog.Parse(canonical).ShouldHaveSingleItem();
        line.Date.ShouldBe(new DateOnly(2026, 10, 4));
        line.Text.ShouldBe("seeded");
        line.Source.ShouldBe(PlanningLogSource.Agent);
    }

    [Fact]
    public void Canonicalize_InlineLogWrapper_SubsequentAppendJoinsExpandedBlock()
    {
        var canonical = PlanningSectionCanonicalizer.Canonicalize(
            "<log>- 2026-10-04 [agent] — seeded</log>");

        var appended = PlanningLog.Append(
            canonical, "next note", new DateTime(2026, 10, 5), PlanningLogSource.Agent);

        appended.ShouldBe("<log>\n- 2026-10-04 [agent] — seeded\n- 2026-10-05 [agent] — next note\n</log>");
        PlanningLog.Parse(appended).Select(l => l.Text).ShouldBe(new[] { "seeded", "next note" });
    }

    [Fact]
    public void Canonicalize_MultiLineLog_LogContentLinesAreUntouched()
    {
        var body = "<log>\n- 2026-10-04 [agent] — first\n- 2026-10-05 [ui] — second\n</log>\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void Canonicalize_ProseMentionsAndStrayTags_AreUntouched()
    {
        var body = """
            Use `<goal>` tags inline, never on their own line.
            see <goal>x</goal> mid-sentence
            <Goal>Upper</Goal>
            - bullet with <scope>y</scope> inside
            """;

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void Canonicalize_CrlfLine_PreservesCarriageReturns()
    {
        PlanningSectionCanonicalizer.Canonicalize("<goal>Ship.</goal>\r\n")
            .ShouldBe("<goal>\r\nShip.\r\n</goal>\r\n");
    }

    [Fact]
    public void Canonicalize_IndentedInlineSection_KeepsIndentOnWrappers()
    {
        var canonical = PlanningSectionCanonicalizer.Canonicalize("intro\n\n  <goal>Ship.</goal>\n");

        canonical.ShouldBe("intro\n\n  <goal>\nShip.\n  </goal>\n");
        PlanningSections.Find("goal", canonical)!.Content.ShouldBe("Ship.");
    }

    [Fact]
    public void Canonicalize_MultipleOneLineSections_AllExpand()
    {
        var canonical = PlanningSectionCanonicalizer.Canonicalize(
            "<goal>A.</goal>\n<scope>B.</scope>\n<refs>- spec 110bcde0</refs>\n");

        canonical.ShouldBe("<goal>\nA.\n</goal>\n<scope>\nB.\n</scope>\n<refs>\n- spec 110bcde0\n</refs>\n");
    }

    [Fact]
    public void Canonicalize_TwoSectionsOnOneLine_IsNotAOneLineSection()
    {
        var body = "<goal>A.</goal> tail <scope>B.</scope>\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void HasInlineSection_MatchesRequestedNameOnly()
    {
        var body = "<goal>\nx\n</goal>\n\n<anti-scope>None.</anti-scope>\n";

        PlanningSectionCanonicalizer.HasInlineSection("anti-scope", body).ShouldBeTrue();
        PlanningSectionCanonicalizer.HasInlineSection("goal", body).ShouldBeFalse();
    }

    [Fact]
    public void HasInlineSection_IgnoresFencesAndProseMentions()
    {
        var fenced = "# Example\n\n```\n<goal>not a section</goal>\n```\n";
        var prose = "see <goal>x</goal> mid-sentence\n";

        PlanningSectionCanonicalizer.HasInlineSection("goal", fenced).ShouldBeFalse();
        PlanningSectionCanonicalizer.HasInlineSection("goal", prose).ShouldBeFalse();
    }

    [Fact]
    public void Canonicalize_NestedBeyondDepthCap_ConvergesOnTheSecondPass()
    {
        var leaf = "<constraints>deep.</constraints>";
        var body = leaf;
        for (var i = 0; i < 9; i++)
        {
            body = $"<scope>{body}</scope>";
        }

        var first = PlanningSectionCanonicalizer.Canonicalize(body);
        PlanningSectionCanonicalizer.HasInlineSection("scope", first).ShouldBeFalse();
        PlanningSectionCanonicalizer.HasInlineSection("constraints", first).ShouldBeTrue();

        var second = PlanningSectionCanonicalizer.Canonicalize(first);
        PlanningSectionCanonicalizer.HasInlineSection("constraints", second).ShouldBeFalse();
        second.ShouldBe(PlanningSectionCanonicalizer.Canonicalize(second));
    }
}
