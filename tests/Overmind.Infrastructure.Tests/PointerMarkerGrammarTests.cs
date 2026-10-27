using Overmind.Core;
using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PointerMarkerGrammarTests
{
    [Theory]
    [InlineData("pointer: docs/plan.md", "docs/plan.md", null)]
    [InlineData("pointer: src/plans/auth-redesign.md#non-goals", "src/plans/auth-redesign.md", "non-goals")]
    [InlineData("pointer: README.md", "README.md", null)]
    [InlineData("  pointer: docs/deep/file.md#sec/two.v3-dash", "docs/deep/file.md", "sec/two.v3-dash")]
    [InlineData("\tpointer: a.md#b", "a.md", "b")]
    [InlineData("pointer: a.md#b#c", "a.md", "b#c")]
    public void ParseLine_ValidForm_ParsesPathAndAnchor(string line, string path, string? anchor)
    {
        var parse = PointerMarkerGrammar.ParseLine(line);

        parse.IsMarker.ShouldBeTrue();
        parse.Error.ShouldBeNull();
        var pointer = parse.Pointer.ShouldNotBeNull();
        pointer.Path.ShouldBe(path);
        pointer.Anchor.ShouldBe(anchor);
    }

    [Theory]
    [InlineData("pointer: grimoire://memory/fc9186e8-e37d-4519-9db6-2de4af8f70d4",
        "grimoire://memory/fc9186e8-e37d-4519-9db6-2de4af8f70d4")]
    [InlineData("  pointer: grimoire://memory/storage-choice", "grimoire://memory/storage-choice")]
    public void ParseLine_GrimoireTarget_ParsesTheUriWholeWithoutAnchor(string line, string path)
    {
        var parse = PointerMarkerGrammar.ParseLine(line);

        parse.IsMarker.ShouldBeTrue();
        parse.Error.ShouldBeNull();
        var pointer = parse.Pointer.ShouldNotBeNull();
        pointer.Path.ShouldBe(path);
        pointer.Anchor.ShouldBeNull();
    }

    [Fact]
    public void ParseLine_UppercaseScheme_IsNotAGrimoirePointer()
    {
        var parse = PointerMarkerGrammar.ParseLine("pointer: Grimoire://memory/abc");

        parse.Pointer.ShouldBeNull();
        parse.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_MultipleLinesAnywhere_CollectsAll()
    {
        var body = "<goal>\nSee the plan.\n</goal>\n\n<plan>\npointer: docs/plans/x.md#implementation\n" +
                   "plain prose\n\n- pointer: docs/other.md\n  pointer: docs/indented.md#anchor\n</plan>\n";

        var pointers = PointerMarkerGrammar.Parse(body);

        pointers.Select(p => p.Path).ShouldBe(["docs/plans/x.md", "docs/indented.md"]);
        pointers[0].Anchor.ShouldBe("implementation");
    }

    [Theory]
    [InlineData("pointer: ../escape.md")]
    [InlineData("pointer: docs/../x.md")]
    [InlineData("pointer: /etc/passwd")]
    [InlineData("pointer: docs\\plan.md")]
    [InlineData("pointer:")]
    [InlineData("pointer:   ")]
    [InlineData("pointer: docs/plan.md#")]
    [InlineData("pointer docs/plan.md")]
    [InlineData("pointer")]
    [InlineData("pointer:docs/plan.md")]
    [InlineData("pointer: a.md b.md")]
    [InlineData("pointer: docs//x.md")]
    [InlineData("pointer: docs/")]
    [InlineData("pointer: #anchor-only")]
    [InlineData("pointer: grimoire://memory/")]
    [InlineData("pointer: grimoire://memory/fc9186e8-e37d-4519-9db6-2de4af8f70d4#today")]
    public void EnsureValid_MalformedMarker_ThrowsTypedInvalidInput(string line)
    {
        var ex = Should.Throw<PlanningException>(() => PointerMarkerGrammar.EnsureValid(line));

        ex.Kind.ShouldBe(PlanningFailureKind.InvalidInput);
        ex.Code.ShouldBe(PlanningFailureCode.InvalidPointerMarker);
    }

    [Fact]
    public void EnsureValid_GrimoireIdMissing_NamesTheMissingMemoryId()
    {
        var ex = Should.Throw<PlanningException>(
            () => PointerMarkerGrammar.EnsureValid("pointer: grimoire://memory/"));

        ex.Message.ShouldContain("the memory id after 'grimoire://memory/' is missing");
    }

    [Fact]
    public void EnsureValid_GrimoireAnchor_SaysAnchorsDoNotApply()
    {
        var ex = Should.Throw<PlanningException>(
            () => PointerMarkerGrammar.EnsureValid("pointer: grimoire://memory/abc#today"));

        ex.Message.ShouldContain("anchors don't apply to grimoire memory pointers");
    }

    [Theory]
    [InlineData("Pointer: docs/x.md")]
    [InlineData("POINTER: docs/x.md")]
    [InlineData("pointers are fun")]
    [InlineData("see pointer: ../x.md")]
    [InlineData("the pointer: convention is neat")]
    [InlineData("pointerfish docs")]
    [InlineData("pointer-ish: docs/x.md")]
    [InlineData("plain prose without markers")]
    public void EnsureValid_LookalikeText_IsNeverValidated(string line)
    {
        Should.NotThrow(() => PointerMarkerGrammar.EnsureValid(line));
        PointerMarkerGrammar.Parse(line).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MalformedLineAmongValidOnes_SkipsOnlyTheMalformed()
    {
        var pointers = PointerMarkerGrammar.Parse("pointer: good.md\npointer: ../bad.md\nprose");

        pointers.Select(p => p.Path).ShouldBe(["good.md"]);
    }

    [Fact]
    public void Parse_GrimoireAmongRepoPointers_CollectsBothShapes()
    {
        var pointers = PointerMarkerGrammar.Parse(
            "pointer: docs/plan.md#notes\n" +
            "pointer: grimoire://memory/fc9186e8-e37d-4519-9db6-2de4af8f70d4\n" +
            "pointer: grimoire://memory/");

        pointers.Select(p => p.Path).ShouldBe([
            "docs/plan.md",
            "grimoire://memory/fc9186e8-e37d-4519-9db6-2de4af8f70d4"
        ]);
        pointers[1].Anchor.ShouldBeNull();
    }

    [Fact]
    public void EnsureValid_MalformedLine_TeachingMessageCarriesLineAndGrammar()
    {
        var ex = Should.Throw<PlanningException>(
            () => PointerMarkerGrammar.EnsureValid("<plan>\npointer: ../x.md\n</plan>"));

        ex.Message.ShouldContain("pointer: ../x.md");
        ex.Message.ShouldContain("'..'");
        ex.Message.ShouldContain("#anchor");
        ex.Message.ShouldContain("'pointer: <repo-relative-path>[#anchor]' or 'pointer: grimoire://memory/<id>'");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EnsureValid_EmptyBody_IsNoOp(string? body)
    {
        Should.NotThrow(() => PointerMarkerGrammar.EnsureValid(body));
        PointerMarkerGrammar.Parse(body).ShouldBeEmpty();
    }
}
