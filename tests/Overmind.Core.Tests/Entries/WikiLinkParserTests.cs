using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entries;

public class WikiLinkParserTests
{
    [Fact]
    public void Parse_FeatureNumber_ProducesTypedFeatureTarget()
    {
        var link = AssertSingle("See [[feature:184]] for the plan.");

        link.Kind.ShouldBe(EntryLinkKind.Feature);
        link.Form.ShouldBe(WikiLinkKeyForm.Number);
        link.Key.ShouldBe("184");
        link.Id.ShouldBeNull();
    }

    [Theory]
    [InlineData("[[feature:007]]", "007")]
    [InlineData("[[feature:184 ]]", "184")]
    public void Parse_FeatureNumberKey_KeepsDigitsVerbatim(string body, string expected)
    {
        var link = AssertSingle(body);

        link.Kind.ShouldBe(EntryLinkKind.Feature);
        link.Form.ShouldBe(WikiLinkKeyForm.Number);
        link.Key.ShouldBe(expected);
    }

    [Fact]
    public void Parse_FeatureSlug_ProducesSlugKey()
    {
        var link = AssertSingle("Bury [[feature:184-obsidian-like-functionality]] now.");

        link.Kind.ShouldBe(EntryLinkKind.Feature);
        link.Form.ShouldBe(WikiLinkKeyForm.Slug);
        link.Key.ShouldBe("184-obsidian-like-functionality");
        link.Id.ShouldBeNull();
    }

    [Fact]
    public void Parse_NonNumericFeatureKey_IsSlugNotNumber()
    {
        var link = AssertSingle("[[feature:184x]]");

        link.Form.ShouldBe(WikiLinkKeyForm.Slug);
    }

    [Fact]
    public void Parse_ProjectName_ProducesNameKey()
    {
        var link = AssertSingle("Cross-ref [[project:grimoire]] here.");

        link.Kind.ShouldBe(EntryLinkKind.Project);
        link.Form.ShouldBe(WikiLinkKeyForm.Name);
        link.Key.ShouldBe("grimoire");
        link.Id.ShouldBeNull();
    }

    [Fact]
    public void Parse_ProjectGuid_ProducesGuidKeyWithId()
    {
        var id = Guid.NewGuid();

        var link = AssertSingle($"[[project:{id}]]");

        link.Kind.ShouldBe(EntryLinkKind.Project);
        link.Form.ShouldBe(WikiLinkKeyForm.Guid);
        link.Key.ShouldBe(id.ToString());
        link.Id.ShouldBe(id);
    }

    [Fact]
    public void Parse_ProjectGuidInBraces_NormalizesKeyAndId()
    {
        var id = Guid.Parse("11112222-3333-4444-5555-666677778888");

        var link = AssertSingle($"[[project:{id:B}]]");

        link.Form.ShouldBe(WikiLinkKeyForm.Guid);
        link.Key.ShouldBe("11112222-3333-4444-5555-666677778888");
        link.Id.ShouldBe(id);
    }

    [Fact]
    public void Parse_BareTitle_ProducesEntryTitle()
    {
        var link = AssertSingle("Read [[Daily standup notes]] first.");

        link.Kind.ShouldBe(EntryLinkKind.Entry);
        link.Form.ShouldBe(WikiLinkKeyForm.Title);
        link.Key.ShouldBe("Daily standup notes");
        link.Id.ShouldBeNull();
    }

    [Fact]
    public void Parse_PrefixIsCaseInsensitive()
    {
        var link = AssertSingle("[[FEATURE:184]]");

        link.Kind.ShouldBe(EntryLinkKind.Feature);
        link.Form.ShouldBe(WikiLinkKeyForm.Number);
    }

    [Fact]
    public void Parse_BacktickFence_SkipsMarkersInside()
    {
        var body = """
                   Before [[feature:1]].

                   ```csharp
                   var x = "[[feature:2]]";
                   [[project:grimoire]]
                   ```

                   After [[feature:3]].
                   """;

        var links = WikiLinkParser.Parse(body);

        links.Select(l => l.Key).ShouldBe(["1", "3"]);
    }

    [Fact]
    public void Parse_TildeFence_SkipsMarkersInside()
    {
        var body = """
                   ~~~
                   [[feature:2]]
                   ~~~
                   [[feature:3]]
                   """;

        var links = WikiLinkParser.Parse(body);

        links.Select(l => l.Key).ShouldBe(["3"]);
    }

    [Fact]
    public void Parse_IndentedFence_StillOpensTheFence()
    {
        var body = """
                   - item
                       ```
                       [[feature:2]]
                       ```
                   - tail [[feature:3]]
                   """;

        var links = WikiLinkParser.Parse(body);

        links.Select(l => l.Key).ShouldBe(["3"]);
    }

    [Fact]
    public void Parse_MixedFenceCharacters_DoNotCloseEachOther()
    {
        var body = """
                   ```
                   [[feature:1]]
                   ~~~
                   [[feature:2]]
                   """;

        WikiLinkParser.Parse(body).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_UnclosedFence_SwallowsTheRestOfTheBody()
    {
        var body = """
                   [[feature:1]]
                   ```
                   [[feature:2]]
                   """;

        var links = WikiLinkParser.Parse(body);

        links.Select(l => l.Key).ShouldBe(["1"]);
    }

    [Fact]
    public void Parse_InlineCodeSpan_SkipsMarkerInside()
    {
        var links = WikiLinkParser.Parse("Use `[[feature:2]]` not [[feature:3]].");

        links.Select(l => l.Key).ShouldBe(["3"]);
    }

    [Fact]
    public void Parse_MultiBacktickInlineCode_SkipsMarkerInside()
    {
        var links = WikiLinkParser.Parse("Try ``[[feature:2]]`` then [[feature:3]].");

        links.Select(l => l.Key).ShouldBe(["3"]);
    }

    [Fact]
    public void Parse_EscapedBacktickRunLengths_KeepSpanningCorrectly()
    {
        var links = WikiLinkParser.Parse("`` a ` b [[feature:1]] `` and [[feature:2]]");

        links.Select(l => l.Key).ShouldBe(["2"]);
    }

    [Fact]
    public void Parse_UnopenedInlineCode_KeepsMarkersAfterTheTicksLive()
    {
        var links = WikiLinkParser.Parse("a ` b [[feature:1]]");

        links.Select(l => l.Key).ShouldBe(["1"]);
    }

    [Fact]
    public void Parse_DuplicateTarget_CollapsesToFirstAppearance()
    {
        var body = """
                   [[feature:1]] then [[ feature:1 ]] then [[project:grimoire]] then [[feature:1]]
                   """;

        var links = WikiLinkParser.Parse(body);

        links.Count.ShouldBe(2);
        links[0].Key.ShouldBe("1");
        links[1].Key.ShouldBe("grimoire");
    }

    [Fact]
    public void Parse_SameNumberAsSlugAndNumber_DoNotCollapse()
    {
        var links = WikiLinkParser.Parse("[[feature:184]] [[project:184]]");

        links.Count.ShouldBe(2);
        links[0].Kind.ShouldBe(EntryLinkKind.Feature);
        links[1].Kind.ShouldBe(EntryLinkKind.Project);
    }

    [Theory]
    [InlineData("[[]]")]
    [InlineData("[[   ]]")]
    [InlineData("[[\t]]")]
    [InlineData("[[feature:]]")]
    [InlineData("[[project:  ]]")]
    public void Parse_EmptyAfterTrim_IsNotALink(string body)
    {
        WikiLinkParser.Parse(body).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MarkerSpanningNewline_IsNotALink()
    {
        var body = """
                   open [[feature:
                   184]] closed
                   """;

        WikiLinkParser.Parse(body).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MultipleMarkers_KeepsBodyOrder()
    {
        var body = """
                   [[project:grimoire]] beats [[feature:184]] beats [[Some Title]]
                   """;

        var links = WikiLinkParser.Parse(body);

        links.Select(l => l.Kind).ShouldBe(
            [EntryLinkKind.Project, EntryLinkKind.Feature, EntryLinkKind.Entry]);
    }

    [Fact]
    public void Parse_TitleKeys_AreCaseSensitiveAndVerbatim()
    {
        var links = WikiLinkParser.Parse("[[Daily]] [[daily]]");

        links.Count.ShouldBe(2);
        links[0].Key.ShouldBe("Daily");
        links[1].Key.ShouldBe("daily");
    }

    [Fact]
    public void Parse_UnmatchedOpenMarkerBeforeClose_SwallowsTheInnerMarker()
    {
        var link = AssertSingle("[[a and [[b]]");

        link.Key.ShouldBe("a and [[b");
    }

    [Fact]
    public void Parse_ClosingMarkerWithoutOpener_IsIgnored()
    {
        WikiLinkParser.Parse("feature:184]]").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_EmptyBody_ReturnsNoLinks()
    {
        WikiLinkParser.Parse(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NullBody_Throws()
    {
        Should.Throw<ArgumentNullException>(() => WikiLinkParser.Parse(null!));
    }

    [Fact]
    public void Parse_NeverInventsGuidsForUnresolvedTargets()
    {
        var body = """
                   [[feature:184]] [[feature:184-slug]] [[project:grimoire]] [[Some Title]]
                   """;

        WikiLinkParser.Parse(body).ShouldAllBe(link => !link.Id.HasValue);
    }

    [Fact]
    public void Parse_CarriageReturnLineEndings_FenceAndMarkersStillParse()
    {
        var links = WikiLinkParser.Parse("```cs\r\n[[feature:1]]\r\n```\r\n[[feature:2]]");

        links.Select(l => l.Key).ShouldBe(["2"]);
    }

    private static WikiLinkTarget AssertSingle(string body)
    {
        var links = WikiLinkParser.Parse(body);
        links.Count.ShouldBe(1);
        return links[0];
    }
}
