using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

public class PlanningLogTests
{
    [Fact]
    public void Append_ExistingSection_InsertsBeforeClose()
    {
        var body = "# Spec\n\nSome content\n\n<log>\n- 2026-01-01 \u2014 old entry\n</log>\n";

        var result = PlanningLog.Append(body, "new entry", new DateTime(2026, 9, 8));

        result.ShouldContain("- 2026-09-08 \u2014 new entry");
        result.ShouldContain("- 2026-01-01 \u2014 old entry");

        var logIdx = result.IndexOf("<log>", StringComparison.Ordinal);
        var newEntryIdx = result.IndexOf("- 2026-09-08", StringComparison.Ordinal);
        var closeIdx = result.IndexOf("</log>", StringComparison.Ordinal);

        newEntryIdx.ShouldBeGreaterThan(logIdx);
        newEntryIdx.ShouldBeLessThan(closeIdx);
    }

    [Fact]
    public void Append_MissingSection_CreatesIt()
    {
        var body = "# Spec\n\nSome content";

        var result = PlanningLog.Append(body, "first note", new DateTime(2026, 9, 8));

        result.ShouldContain("<log>");
        result.ShouldContain("</log>");
        result.ShouldContain("- 2026-09-08 \u2014 first note");
    }

    [Fact]
    public void Append_EmptyBody_CreatesSection()
    {
        var result = PlanningLog.Append("", "note", new DateTime(2026, 9, 8));

        result.ShouldContain("<log>");
        result.ShouldContain("- 2026-09-08 \u2014 note");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Append_EmptyNote_Throws(string? note)
    {
        Should.Throw<ArgumentException>(() => PlanningLog.Append("body", note!));
    }

    [Fact]
    public void Parse_ReadsDatesAndText()
    {
        var body = "content\n\n<log>\n- 2026-09-08 \u2014 first note\n- 2026-01-15 \u2014 second note\n</log>\n";

        var lines = PlanningLog.Parse(body);

        lines.Count.ShouldBe(2);
        lines[0].Date.ShouldBe(new DateOnly(2026, 9, 8));
        lines[0].Text.ShouldBe("first note");
        lines[1].Date.ShouldBe(new DateOnly(2026, 1, 15));
        lines[1].Text.ShouldBe("second note");
    }

    [Fact]
    public void Parse_MissingSection_ReturnsEmpty()
    {
        var lines = PlanningLog.Parse("no log section here");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_EmptyBody_ReturnsEmpty()
    {
        var lines = PlanningLog.Parse("");

        lines.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MalformedLine_DateIsNull_TextKept()
    {
        var body = "<log>\n- not-a-date some text\n- 2026-09-08 \u2014 good line\n</log>";

        var lines = PlanningLog.Parse(body);

        lines.Count.ShouldBe(2);
        lines[0].Date.ShouldBeNull();
        lines[0].Text.ShouldContain("not-a-date");
        lines[1].Date.ShouldBe(new DateOnly(2026, 9, 8));
        lines[1].Text.ShouldBe("good line");
    }

    [Fact]
    public void Parse_IgnoresNonDashLines()
    {
        var body = "<log>\nsome random text\n- 2026-09-08 \u2014 valid\n</log>";

        var lines = PlanningLog.Parse(body);

        lines.Count.ShouldBe(1);
        lines[0].Date.ShouldBe(new DateOnly(2026, 9, 8));
    }

    [Fact]
    public void Append_ProseMentionWithRealSection_NoteLandsInSection()
    {
        var body = "# Plan\n\nUse `<log>` tags to track changes.\nSee `</log>` for format.\n\n<log>\n- 2026-01-01 \u2014 original entry\n</log>\n";
        var proseLen = body.IndexOf("<log>\n- 2026-01-01", StringComparison.Ordinal);

        var result = PlanningLog.Append(body, "new note", new DateTime(2026, 9, 8));

        result.ShouldContain("- 2026-09-08 \u2014 new note");
        var lastOpenIdx = result.LastIndexOf("<log>", StringComparison.Ordinal);
        var lastCloseIdx = result.LastIndexOf("</log>", StringComparison.Ordinal);
        var noteIdx = result.IndexOf("- 2026-09-08", StringComparison.Ordinal);
        noteIdx.ShouldBeGreaterThan(lastOpenIdx);
        noteIdx.ShouldBeLessThan(lastCloseIdx);

        var proseSection = result[..proseLen];
        proseSection.ShouldContain("`<log>` tags to track changes");
        proseSection.ShouldContain("`</log>` for format");
    }

    [Fact]
    public void Append_ProseMentionWithoutSection_CreatesAtEnd()
    {
        var body = "# Plan\n\nUse `<log>` tags to track changes.\nSee `</log>` for format.";

        var result = PlanningLog.Append(body, "first note", new DateTime(2026, 9, 8));

        result.ShouldContain("`<log>` tags to track changes");
        result.ShouldContain("`</log>` for format");

        var lastOpenIdx = result.LastIndexOf("<log>\n", StringComparison.Ordinal);
        var proseEndIdx = result.IndexOf("for format.", StringComparison.Ordinal);
        lastOpenIdx.ShouldBeGreaterThan(proseEndIdx);

        result.ShouldContain("- 2026-09-08 \u2014 first note");
        result.ShouldEndWith("</log>\n");
    }

    [Fact]
    public void Append_MultipleSections_OperatesOnLast()
    {
        var body = "<log>\n- 2026-01-01 \u2014 old\n</log>\n\nSome text\n\n<log>\n- 2026-06-01 \u2014 newer\n</log>\n";

        var result = PlanningLog.Append(body, "latest", new DateTime(2026, 9, 8));

        var firstCloseIdx = result.IndexOf("</log>", StringComparison.Ordinal);
        var lastOpenIdx = result.LastIndexOf("<log>", StringComparison.Ordinal);
        var noteIdx = result.IndexOf("- 2026-09-08", StringComparison.Ordinal);

        noteIdx.ShouldBeGreaterThan(lastOpenIdx);
        firstCloseIdx.ShouldBeLessThan(lastOpenIdx);
    }

    [Fact]
    public void Append_NeverProducesCarriageReturn()
    {
        var body = "# Spec\n\nContent\n\n<log>\n- 2026-01-01 \u2014 old\n</log>\n";

        var result = PlanningLog.Append(body, "note", new DateTime(2026, 9, 8));

        result.ShouldNotContain("\r");
    }

    [Fact]
    public void Parse_ProseOnlyBody_ReturnsEmpty()
    {
        var body = "# Plan\n\nUse `<log>` tags to track changes.\nSee `</log>` for format.";

        var lines = PlanningLog.Parse(body);

        lines.ShouldBeEmpty();
    }
}
