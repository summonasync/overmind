using System.Text.RegularExpressions;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

public class PlanningHeadingSectionsTests
{
    private const string SpecBody = """
# Spec

## Scope
in scope
### Deep
still scope

## Next
after scope

## Tail
end
""";

    [Fact]
    public void Find_SpansTheHeadingThroughTheNextSameOrHigherHeading()
    {
        var block = PlanningHeadingSections.Find("Scope", SpecBody).ShouldHaveSingleItem();

        block.HeadingLine.ShouldBe("## Scope");
        SpecBody[block.Start..block.End].ShouldBe("## Scope\nin scope\n### Deep\nstill scope\n\n");
        block.EndsWithNewline.ShouldBeTrue();
    }

    [Fact]
    public void Find_BareAndPrefixedAddresses_ReducesToTheSameHeadingText()
    {
        PlanningHeadingSections.TryGetHeadingText("## Scope", out var prefixed).ShouldBeTrue();
        PlanningHeadingSections.TryGetHeadingText("Scope", out var bare).ShouldBeTrue();
        PlanningHeadingSections.TryGetHeadingText("###Scope", out var overPrefixed).ShouldBeTrue();

        prefixed.ShouldBe(bare);
        prefixed.ShouldBe(overPrefixed);
        PlanningHeadingSections.Find(prefixed, SpecBody).ShouldHaveSingleItem();
        PlanningHeadingSections.Find(bare, SpecBody).ShouldHaveSingleItem();
    }

    [Fact]
    public void TryGetHeadingText_HashesWithoutText_IsRefused()
    {
        PlanningHeadingSections.TryGetHeadingText("##", out _).ShouldBeFalse();
        PlanningHeadingSections.TryGetHeadingText("###", out _).ShouldBeFalse();
    }

    [Fact]
    public void Find_H1TerminatesTheBlock()
    {
        const string body = "## A\ntext\n# Top\ntail";

        var block = PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem();

        body[block.Start..block.End].ShouldBe("## A\ntext\n");
    }

    [Fact]
    public void Find_IgnoresHeadingsInsideFences()
    {
        const string body = "## A\n```md\n## not a heading\n```\nstill in A\n## B\nafter";

        var block = PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem();

        body[block.Start..block.End].ShouldBe("## A\n```md\n## not a heading\n```\nstill in A\n");
        PlanningHeadingSections.List(body).ShouldBe(["## A", "## B"]);

        const string tilded = "## A\n~~~\n## not a heading\n~~~\nstill in A\n## B\nafter";

        var tildeBlock = PlanningHeadingSections.Find("A", tilded).ShouldHaveSingleItem();

        tilded[tildeBlock.Start..tildeBlock.End].ShouldBe("## A\n~~~\n## not a heading\n~~~\nstill in A\n");
        PlanningHeadingSections.List(tilded).ShouldBe(["## A", "## B"]);
    }

    [Fact]
    public void Find_AtTheBodyTail_TracksWhetherTheBodyEndsWithANewline()
    {
        PlanningHeadingSections.Find("A", "## A\ntext").ShouldHaveSingleItem().EndsWithNewline.ShouldBeFalse();
        PlanningHeadingSections.Find("A", "## A\ntext\n").ShouldHaveSingleItem().EndsWithNewline.ShouldBeTrue();
    }

    [Fact]
    public void Find_Missing_ReturnsEmpty_Duplicates_ReturnsBoth()
    {
        PlanningHeadingSections.Find("Absent", SpecBody).ShouldBeEmpty();

        const string duplicated = "## Scope\na\nmiddle\n## Scope\nb";
        PlanningHeadingSections.Find("Scope", duplicated).Count.ShouldBe(2);
    }

    [Fact]
    public void Replace_KeepsTheHeadingLineByteIdentical_IncludingTrailingWhitespace()
    {
        const string body = "## Scope \nold lines\n## B\nafter";

        var replaced = PlanningHeadingSections.Replace(
            PlanningHeadingSections.Find("Scope", body).ShouldHaveSingleItem(), body, "new lines");

        replaced.ShouldBe("## Scope \nnew lines\n## B\nafter");
    }

    [Fact]
    public void Replace_AtTheBodyTail_PreservesTheTrailingNewline()
    {
        var block = PlanningHeadingSections.Find("A", "## A\nold\n").ShouldHaveSingleItem();
        var withoutNewline = PlanningHeadingSections.Find("A", "## A\nold").ShouldHaveSingleItem();

        PlanningHeadingSections.Replace(block, "## A\nold\n", "new").ShouldBe("## A\nnew\n");
        PlanningHeadingSections.Replace(withoutNewline, "## A\nold", "new").ShouldBe("## A\nnew");
    }

    [Fact]
    public void Replace_WithTheOriginalContent_IsByteIdentical()
    {
        const string body = "## Scope\nin scope\n### Deep\nstill scope\n## Next\nafter";
        var block = PlanningHeadingSections.Find("Scope", body).ShouldHaveSingleItem();

        PlanningHeadingSections.Replace(block, body, "in scope\n### Deep\nstill scope").ShouldBe(body);
    }

    [Fact]
    public void Replace_WithEmptyContent_ClearsTheBlockUnderTheHeading()
    {
        const string body = "## A\nold\n## B\nafter";

        var replaced = PlanningHeadingSections.Replace(
            PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem(), body, string.Empty);

        replaced.ShouldBe("## A\n## B\nafter");
    }

    [Fact]
    public void Replace_CrlfBody_KeepsCarriageReturnEndingsAcrossTheSplice()
    {
        const string body = "## A\r\nold\r\n## B\r\ntail";
        var block = PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem();

        PlanningHeadingSections.Replace(block, body, "new").ShouldBe("## A\r\nnew\r\n## B\r\ntail");
        PlanningHeadingSections.Replace(block, body, "one\ntwo").ShouldBe("## A\r\none\r\ntwo\r\n## B\r\ntail");
    }

    [Fact]
    public void Replace_CrlfContent_OnCrlfBody_IsByteIdentical()
    {
        const string body = "## A\r\nold\r\nmore\r\n## B\r\ntail";
        var block = PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem();

        PlanningHeadingSections.Replace(block, body, "old\r\nmore").ShouldBe(body);
    }

    [Fact]
    public void Insert_AppendsTheHeadingSectionAtTheEnd()
    {
        PlanningHeadingSections.Insert("Scope", "intro\n", "line one\nline two")
            .ShouldBe("intro\n## Scope\nline one\nline two\n");
        PlanningHeadingSections.Insert("Scope", "intro", "line one")
            .ShouldBe("intro\n## Scope\nline one\n");
    }

    [Fact]
    public void Insert_WithoutContent_UsesTheDeclaredEmptyMarker()
    {
        PlanningHeadingSections.Insert("Scope", "intro\n", null).ShouldBe("intro\n## Scope\n(none)\n");
        PlanningHeadingSections.Insert("Scope", "intro\n", "   \n").ShouldBe("intro\n## Scope\n(none)\n");
    }

    [Fact]
    public void Remove_ReplacesTheWholeBlockIncludingTheHeadingLineWithTheMarker()
    {
        const string body = "before\n\n## A\nold lines\nmore\n## B\ntail";
        var marker = "<removed 2026-10-28 — scope cut in review>";

        var removed = PlanningHeadingSections.Remove(
            PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem(), body, marker);

        removed.ShouldBe("before\n\n<removed 2026-10-28 — scope cut in review>\n## B\ntail");
    }

    [Fact]
    public void Remove_CrlfBody_WritesTheTombstoneWithTheBodySNewline()
    {
        const string body = "## A\r\nold\r\n## B\r\ntail";
        var marker = "<removed 2026-10-28 — scope cut in review>";

        var removed = PlanningHeadingSections.Remove(
            PlanningHeadingSections.Find("A", body).ShouldHaveSingleItem(), body, marker);

        removed.ShouldBe("<removed 2026-10-28 — scope cut in review>\r\n## B\r\ntail");
    }

    [Fact]
    public void TombstoneLine_CarriesTheUtcDateAndTheVerbatimReason()
    {
        var line = PlanningHeadingSections.TombstoneLine("scope cut in review");

        Regex.IsMatch(line, @"^<removed \d{4}-\d{2}-\d{2} — scope cut in review>$").ShouldBeTrue();
    }

    [Theory]
    [InlineData("goal", false)]
    [InlineData("anti-scope", false)]
    [InlineData("Scope", true)]
    [InlineData("## Scope", true)]
    [InlineData("has_underscore", true)]
    public void IsAddress_RoutesTagNamesToTheXmlPathAndEverythingElseToHeadings(string section, bool expected)
    {
        PlanningHeadingSections.IsAddress(section).ShouldBe(expected);
    }
}
