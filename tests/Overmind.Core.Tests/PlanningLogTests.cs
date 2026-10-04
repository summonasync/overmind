using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

public class PlanningLogTests
{
    private const string Task038Body = """
<goal>
Priority changes inline in one click from the board card and the feature workspace header — no editor modal — with optimistic update, rollback on failure, and immediate board re-sort.
</goal>

<context>
022 precedent (status select on board cards + workspace header with optimistic update and snapshot rollback) is the interaction vocabulary; 034 landed the priority types (PlanningFeaturePriority, FEATURE_PRIORITIES), priorityTone mapping, the composite board sort, and the editor select with the PUT path (api.ts sends priority: values.priority ?? null — null = untouched). Spec artifact 63d8282e has the decisions.
Files: web/src/features/planning/components/feature-board.tsx (card metadata row — the priority chip added by 034), feature-workspace.tsx (header next to the status select), possibly a small shared priority-select component if both sites would duplicate markup, api.ts mutations if wiring needs it.
</context>

<scope>
- Board card: replace the static priority chip with an interactive mini-select — chip-toned look (p0 danger / p1 warning / p2 neutral / p3 muted via priorityTone), options P0-P3, immediate PUT through the existing update mutation, optimistic cache update, rollback on failure (022 snapshot-rollback path), disabled while mutation pending.
- Workspace header: priority select beside the status select, same behavior and sizing.
- Shared component in features/planning if the two call sites would otherwise duplicate markup (export via index.ts).
- Board must re-sort immediately on success (existing composite sort reacts to the cache update — verify, don't add).
- Gates: cd web && npm run lint && npx tsc --noEmit && npm run build — all green.
</scope>

<anti-scope>
- No backend changes, no API changes.
- No agent-door priority UI, no DnD, no batch re-prioritization.
- No changes to the editor form select (it stays).
</anti-scope>

<constraints>
- FSD boundaries, React Query patterns, no useEffect fetching, no new packages.
- Match the status-select's exact interaction details (rollback, pending-disable, a11y).
- No git commits — report changed files.
</constraints>

<acceptance-criteria>
- Card + workspace header both change priority in one interaction; board re-sorts immediately.
- Failed PUT rolls the select back to the previous value.
- Select disabled during flight; lint/tsc/build green.
</acceptance-criteria>

<refs>
- Spec artifact 63d8282e (022-mirror decisions).
- 034/03 web work (types, tones, sort, PUT semantics).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Spec gated (user approve 2026-09-20 night). Starting execution: next-js-coder (sonnet) on BACKLOG/038-priority-inline-edit/tasks/01-task-01.md.
- 2026-09-20 — Review gate passed: PASS, 0 BLOCKER/WARNING (audits/review-038-01.md). Gates green (lint/tsc/build). 022-pattern fidelity verified point-by-point (dual snapshot, rollback guards, dual-key invalidation, pending-disable on both surfaces); tone-class extraction byte-identical for the status select; instant re-sort confirmed (no memoization on the sort path).
</log>
""";

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
    public void Append_InlineWrapperOnly_SplicesInsideInsteadOfSecondBlock()
    {
        var body = "# Task\n\n<log>- 2026-10-04 [controller] filed as a task per crew rule</log>\n";

        var result = PlanningLog.Append(body, "claimed by Van", new DateTime(2026, 10, 4));

        CountTokens(result, "<log>").ShouldBe(1);
        CountTokens(result, "</log>").ShouldBe(1);
        result.ShouldContain("- 2026-10-04 [controller] filed as a task per crew rule");
        result.ShouldContain("- 2026-10-04 \u2014 claimed by Van");
        result.IndexOf("- 2026-10-04 \u2014 claimed by Van", StringComparison.Ordinal)
            .ShouldBeLessThan(result.IndexOf("</log>", StringComparison.Ordinal));
    }

    [Fact]
    public void Append_InlineWrapper_RepeatedAppendsStayInSingleWrapper()
    {
        var body = "# Task\n\n<log>- 2026-10-04 [controller] filed</log>\n";

        var once = PlanningLog.Append(body, "first", new DateTime(2026, 10, 4));
        var twice = PlanningLog.Append(once, "second", new DateTime(2026, 10, 5));

        CountTokens(twice, "<log>").ShouldBe(1);
        CountTokens(twice, "</log>").ShouldBe(1);
        twice.ShouldContain("- 2026-10-04 \u2014 first");
        twice.ShouldContain("- 2026-10-05 \u2014 second");
    }

    [Fact]
    public void Append_ProseLogMentionWithoutClose_CreatesBlockSection()
    {
        var body = "# Task\n\n- recorded in this artifact's <log> on the board";

        var result = PlanningLog.Append(body, "note", new DateTime(2026, 10, 4));

        CountTokens(result, "</log>").ShouldBe(1);
        result.ShouldContain("- 2026-10-04 \u2014 note");
        result.ShouldContain("recorded in this artifact's <log> on the board");
    }

    private static int CountTokens(string content, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
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

    [Fact]
    public void Append_BodyWithoutTrailingNewline_PrependsBlankLineSeparator()
    {
        var result = PlanningLog.Append("Some content", "note", new DateTime(2026, 9, 8));

        result.ShouldBe("Some content\n\n<log>\n- 2026-09-08 — note\n</log>\n");
    }

    [Fact]
    public void Append_BodyWithTrailingNewline_AlsoYieldsBlankLineBeforeSection()
    {
        var result = PlanningLog.Append("Some content\n", "note", new DateTime(2026, 9, 8));

        result.ShouldBe("Some content\n\n<log>\n- 2026-09-08 — note\n</log>\n");
    }

    [Fact]
    public void Append_EmptyBody_ProducesBareSection()
    {
        var result = PlanningLog.Append("", "note", new DateTime(2026, 9, 8));

        result.ShouldBe("<log>\n- 2026-09-08 — note\n</log>\n");
    }

    [Fact]
    public void Append_ExistingSection_ExactOutputPinned()
    {
        var body = "# Spec\n\n<log>\n- 2026-01-01 — old\n</log>\n";

        var result = PlanningLog.Append(body, "new", new DateTime(2026, 9, 8));

        result.ShouldBe("# Spec\n\n<log>\n- 2026-01-01 — old\n- 2026-09-08 — new\n</log>\n");
    }

    [Fact]
    public void Parse_MultipleSections_ReadsLastAnchoredPairOnly()
    {
        var body = "<log>\n- 2026-01-01 — first\n</log>\n\nmiddle\n\n<log>\n- 2026-06-01 — second\n</log>\n";

        var lines = PlanningLog.Parse(body);

        lines.Count.ShouldBe(1);
        lines[0].Date.ShouldBe(new DateOnly(2026, 6, 1));
        lines[0].Text.ShouldBe("second");
    }

    [Fact]
    public void Append_RealTaskBody_AddsDatedEntryToLastLog()
    {
        PlanningLog.Parse(Task038Body).Count.ShouldBe(2);

        var result = PlanningLog.Append(Task038Body, "039 regression pin", new DateTime(2026, 9, 22));

        var lines = PlanningLog.Parse(result);
        lines.Count.ShouldBe(3);
        lines[2].Date.ShouldBe(new DateOnly(2026, 9, 22));
        lines[2].Text.ShouldBe("039 regression pin");
        result.ShouldEndWith("- 2026-09-22 — 039 regression pin\n</log>");
    }

    [Fact]
    public void Append_CrlfLogSection_InsertsInsideExistingPairWithLfLine()
    {
        var body = "# Spec\r\n\r\n<log>\r\n- 2026-01-01 — old\r\n</log>\r\n";

        var result = PlanningLog.Append(body, "note", new DateTime(2026, 9, 22));

        result.ShouldContain("- 2026-01-01 — old\r\n- 2026-09-22 — note\n</log>\r\n");
        PlanningLog.Parse(result).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(PlanningLogSource.Agent, "agent")]
    [InlineData(PlanningLogSource.Ui, "ui")]
    [InlineData(PlanningLogSource.Auto, "auto")]
    [InlineData(PlanningLogSource.Import, "import")]
    public void Append_WithSource_EmitsMarkerAfterDate(PlanningLogSource source, string marker)
    {
        var result = PlanningLog.Append("", "note", new DateTime(2026, 9, 22), source);

        result.ShouldBe($"<log>\n- 2026-09-22 [{marker}] — note\n</log>\n");
    }

    [Theory]
    [InlineData(PlanningLogSource.Agent)]
    [InlineData(PlanningLogSource.Ui)]
    [InlineData(PlanningLogSource.Auto)]
    [InlineData(PlanningLogSource.Import)]
    public void Append_WithSource_RoundTripsThroughParse(PlanningLogSource source)
    {
        var result = PlanningLog.Append("body", "moved by hand", new DateTime(2026, 9, 22), source);

        var lines = PlanningLog.Parse(result);

        lines.ShouldHaveSingleItem();
        lines[0].Date.ShouldBe(new DateOnly(2026, 9, 22));
        lines[0].Text.ShouldBe("moved by hand");
        lines[0].Source.ShouldBe(source);
    }

    [Fact]
    public void Append_WithoutSource_StillProducesLegacyLine()
    {
        var result = PlanningLog.Append("", "note", new DateTime(2026, 9, 22));

        result.ShouldBe("<log>\n- 2026-09-22 — note\n</log>\n");
        PlanningLog.Parse(result)[0].Source.ShouldBeNull();
    }

    [Fact]
    public void Parse_LegacyBody_AllSourcesNull()
    {
        var body = "<log>\n- 2026-01-01 — old entry\n- 2026-06-01 — newer entry\n</log>\n";

        var lines = PlanningLog.Parse(body);

        lines.Count.ShouldBe(2);
        lines.ShouldAllBe(l => l.Source == null);
    }

    [Fact]
    public void Parse_MixedMarkerAndLegacyLines_ReadsBoth()
    {
        var body = "<log>\n- 2026-01-01 — legacy entry\n- 2026-06-01 [agent] — sourced entry\n</log>\n";

        var lines = PlanningLog.Parse(body);

        lines[0].Source.ShouldBeNull();
        lines[0].Text.ShouldBe("legacy entry");
        lines[1].Source.ShouldBe(PlanningLogSource.Agent);
        lines[1].Text.ShouldBe("sourced entry");
    }

    [Theory]
    [InlineData("[user]")]
    [InlineData("[42]")]
    [InlineData("[1]")]
    [InlineData("[]")]
    public void Parse_UnknownMarker_SourceIsNull(string marker)
    {
        var body = $"<log>\n- 2026-06-01 {marker} — entry\n</log>\n";

        var lines = PlanningLog.Parse(body);

        lines.ShouldHaveSingleItem();
        lines[0].Source.ShouldBeNull();
    }

    [Fact]
    public void Append_SourceFromParsedLine_ReplaysIdenticalBody()
    {
        var original = PlanningLog.Append("body", "note", new DateTime(2026, 9, 22), PlanningLogSource.Import);

        var replayed = PlanningLog.Append(
            "body",
            PlanningLog.Parse(original)[0].Text,
            new DateTime(2026, 9, 22),
            PlanningLog.Parse(original)[0].Source);

        replayed.ShouldBe(original);
    }
}
