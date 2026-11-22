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
    public void Canonicalize_ConcatenatedGroups_AllExpandInOrder()
    {
        var body = "<goal>G.</goal><scope>S.</scope><anti-scope>AS.</anti-scope>"
            + "<constraints>C.</constraints><acceptance-criteria>[x] one</acceptance-criteria><refs>- spec ab</refs>\n";

        var canonical = PlanningSectionCanonicalizer.Canonicalize(body);

        canonical.ShouldBe(
            "<goal>\nG.\n</goal>\n<scope>\nS.\n</scope>\n<anti-scope>\nAS.\n</anti-scope>\n"
            + "<constraints>\nC.\n</constraints>\n<acceptance-criteria>\n[x] one\n</acceptance-criteria>\n"
            + "<refs>\n- spec ab\n</refs>\n");
        PlanningSections.List(canonical).ShouldBe(
            new[] { "goal", "scope", "anti-scope", "constraints", "acceptance-criteria", "refs" });
        PlanningSections.Find("acceptance-criteria", canonical)!.Content.ShouldBe("[x] one");
    }

    [Fact]
    public void Canonicalize_WhitespaceBetweenGroups_ExpandsBoth()
    {
        var canonical = PlanningSectionCanonicalizer.Canonicalize("<goal>A.</goal> <scope>B.</scope>\n");

        canonical.ShouldBe("<goal>\nA.\n</goal>\n<scope>\nB.\n</scope>\n");
        PlanningSections.List(canonical).ShouldBe(new[] { "goal", "scope" });
    }

    [Fact]
    public void Canonicalize_ConcatenatedGroups_CarriageReturnRidesTheLastExpansion()
    {
        PlanningSectionCanonicalizer.Canonicalize("<goal>A.</goal><scope>B.</scope>\r\n")
            .ShouldBe("<goal>\nA.\n</goal>\n<scope>\r\nB.\r\n</scope>\r\n");
    }

    [Fact]
    public void Canonicalize_ConcatenatedGroupsInsideFence_AreUntouched()
    {
        var body = "```\n<goal>a</goal><scope>b</scope>\n```\n<log>\n- note\n</log>\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void Canonicalize_NestedConcatenatedGroups_FullyExpandInOnePass()
    {
        var once = PlanningSectionCanonicalizer.Canonicalize("<goal><scope>a</scope><refs>r</refs></goal>");

        once.ShouldBe("<goal>\n<scope>\na\n</scope>\n<refs>\nr\n</refs>\n</goal>");
        PlanningSectionCanonicalizer.Canonicalize(once).ShouldBe(once);
    }

    [Fact]
    public void Canonicalize_ProseLineWithEmbeddedTags_IsUntouched()
    {
        var body = "use <b>bold</b> here\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void HasInlineSection_FindsNamesInsideConcatenatedLines()
    {
        var body = "<goal>a</goal><scope>b</scope>\n";

        PlanningSectionCanonicalizer.HasInlineSection("scope", body).ShouldBeTrue();
        PlanningSectionCanonicalizer.HasInlineSection("goal", body).ShouldBeTrue();
        PlanningSectionCanonicalizer.HasInlineSection("refs", body).ShouldBeFalse();
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

    [Fact]
    public void Canonicalize_GroupRunPastPerLineCap_StaysRaw()
    {
        var body = string.Concat(Enumerable.Repeat("<note>x</note>", 300));

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
        PlanningSectionCanonicalizer.HasInlineSection("note", body).ShouldBeFalse();
        PlanningSectionCanonicalizer.Canonicalize("<note>x</note><goal>g</goal>")
            .ShouldBe("<note>\nx\n</note>\n<goal>\ng\n</goal>");
    }

    // The measured 5b846b1a repro: authored task bodies whose closing tags are glued to the
    // last content line. The anchored-close parser misses those sections, the task skeleton
    // appends empty (none) tail copies, and last-anchored-pair resolution serves the empty
    // copies — AC ticks 400 and acTotal reads 0.
    [Fact]
    public void Canonicalize_GluedClosers_HealOntoTheirOwnLines()
    {
        var body = "<goal>\nGive the product one numeric grammar.</goal>\n\n<scope>\n- line one\n</scope>\n\n<anti-scope>\nNo doors here.</anti-scope>\n";

        var healed = PlanningSectionCanonicalizer.Canonicalize(body);

        healed.ShouldBe(
            "<goal>\nGive the product one numeric grammar.\n</goal>\n\n<scope>\n- line one\n</scope>\n\n<anti-scope>\nNo doors here.\n</anti-scope>\n");
        PlanningSections.TryFind("goal", healed, out _).ShouldBeTrue();
        PlanningSections.TryFind("scope", healed, out _).ShouldBeTrue();
        PlanningSections.TryFind("anti-scope", healed, out _).ShouldBeTrue();
    }

    [Fact]
    public void Canonicalize_GluedClosers_HealedBodyIsIdempotent()
    {
        var body = "<acceptance-criteria>\n- [ ] build green\n- [ ] tests green</acceptance-criteria>\n";

        var healed = PlanningSectionCanonicalizer.Canonicalize(body);

        healed.ShouldBe("<acceptance-criteria>\n- [ ] build green\n- [ ] tests green\n</acceptance-criteria>\n");
        PlanningSectionCanonicalizer.Canonicalize(healed).ShouldBe(healed);
    }

    [Fact]
    public void Canonicalize_GluedCloserInsideFence_StaysRaw()
    {
        var body = "<goal>\nopen\n```\nnot a section</goal>\n```\n</goal>\n";

        var healed = PlanningSectionCanonicalizer.Canonicalize(body);

        healed.ShouldContain("not a section</goal>");
        healed.ShouldNotContain("not a section\n</goal>");
    }

    [Fact]
    public void Canonicalize_GluedCloserWithTrailingProse_StaysRaw()
    {
        var body = "<goal>\ncontent</goal> and more\n</goal>\n";

        PlanningSectionCanonicalizer.Canonicalize(body).ShouldBe(body);
    }

    [Fact]
    public void WithSkeleton_GluedCloserBody_GetsNoDuplicateTailSections()
    {
        var poisoned = "<goal>\nreal goal.</goal>\n\n<anti-scope>\nreal scope.</anti-scope>\n\n<acceptance-criteria>\n- [ ] build green\n- [ ] tests green</acceptance-criteria>\n";

        var skeleton = PlanningTaskSections.WithSkeleton(PlanningSectionCanonicalizer.Canonicalize(poisoned));

        PlanningSections.CountAnchoredBlocks("goal", skeleton).ShouldBe(1);
        PlanningSections.CountAnchoredBlocks("anti-scope", skeleton).ShouldBe(1);
        PlanningSections.CountAnchoredBlocks("acceptance-criteria", skeleton).ShouldBe(1);
        PlanningSections.ParseAcceptanceCriteria(skeleton).Count.ShouldBe(2);
    }
}
