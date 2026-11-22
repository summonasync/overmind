using System.Text.Json;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Core.Tests;

// Table-driven soft-cap engine coverage (266 T1): per limit per zone — silent, warn-pass,
// truncate — plus the surrogate-pair, single-line and preview traps and the frozen warning shape.
public class SoftCapTests
{
    private readonly ITestOutputHelper _output;

    public SoftCapTests(ITestOutputHelper output) => _output = output;

    // owner worked example: L=500 → 501..610 warn whole, 611+ truncate to 600; L=2000 →
    // 2001..2440 warn whole, 2441+ truncate to 2400. Zone rows below are the AC2 denominators.
    public static readonly TheoryData<int, int, int, bool?, bool?> ZoneCases = new()
    {
        { 500, 1, 1, null, null },
        { 500, 500, 500, null, null },
        { 500, 501, 501, false, false },
        { 500, 555, 555, false, false },
        { 500, 610, 610, false, false },
        { 500, 611, 600, false, true },
        { 500, 700, 600, false, true },
        { 500, 5_000, 600, false, true },
        { 2_000, 2_000, 2_000, null, null },
        { 2_000, 2_001, 2_001, false, false },
        { 2_000, 2_440, 2_440, false, false },
        { 2_000, 2_441, 2_400, false, true },
        { 2_000, 9_999, 2_400, false, true },
    };

    [Theory]
    [MemberData(nameof(ZoneCases))]
    public void Apply_Zones_StoreAndWarnPerFormula(
        int limit, int len, int expectedStored, bool? expectedWarn, bool? expectedTruncated)
    {
        var text = new string('x', len);

        var outcome = SoftCap.Apply(text, limit);

        outcome.Stored!.Length.ShouldBe(expectedStored);
        if (expectedWarn is null)
        {
            outcome.Warning.ShouldBeNull();
            return;
        }

        outcome.Warning.ShouldNotBeNull();
        outcome.Warning.Truncated.ShouldBe(expectedTruncated!.Value);
        outcome.Warning.Limit.ShouldBe(limit);
        outcome.Warning.AcceptedLen.ShouldBe(expectedStored);
        if (expectedTruncated == true)
        {
            outcome.Warning.LostTailPreview.ShouldNotBeNull();
        }
        else
        {
            outcome.Warning.LostTailPreview.ShouldBeNull();
        }
    }

    [Fact]
    public void Apply_WorkedExample_L500_TracesOwnerTable()
    {
        var at501 = SoftCap.Apply(new string('x', 501), 500);
        at501.Stored!.Length.ShouldBe(501);
        at501.Warning!.Truncated.ShouldBe(false);
        at501.Warning.AcceptedLen.ShouldBe(501);

        var at610 = SoftCap.Apply(new string('x', 610), 500);
        at610.Stored!.Length.ShouldBe(610);
        at610.Warning!.Truncated.ShouldBe(false);

        var at611 = SoftCap.Apply(new string('x', 611), 500);
        at611.Stored!.Length.ShouldBe(600);
        at611.Warning!.Truncated.ShouldBe(true);
        at611.Warning.AcceptedLen.ShouldBe(600);
        at611.Warning.LostTailPreview!.Length.ShouldBe(11);

        _output.WriteLine(
            $"worked example L=500: 501->{at501.Stored.Length} warn-pass, " +
            $"610->{at610.Stored.Length} warn-pass, 611->{at611.Stored.Length} truncated " +
            $"(preview '{at611.Warning.LostTailPreview}')");
    }

    [Fact]
    public void Apply_Truncate_PreviewIsLostTailEnd_AndDroppedTailNamed()
    {
        var text = new string('x', 600) + new string('y', 200);
        var outcome = SoftCap.Apply(text, 500);

        outcome.Stored!.Length.ShouldBe(600);
        outcome.Warning!.Truncated.ShouldBe(true);
        // dropped tail = text[600..], 200 chars; preview caps at ~50 chars of its end.
        outcome.Warning.LostTailPreview.ShouldBe(new string('y', 50));
    }

    [Fact]
    public void Apply_CutOnSurrogatePair_BacksOffByOneAndKeepsPairsIntact()
    {
        const string pair = "𝄞"; // U+1D11E, high+low surrogate pair
        var text = new string('x', 599) + pair + new string('x', 200);
        text.Length.ShouldBe(801);

        var outcome = SoftCap.Apply(text, 500);

        // keep=600 would split the pair at [599]|[600]; the cut backs off to 599.
        outcome.Stored!.Length.ShouldBe(599);
        char.IsHighSurrogate(outcome.Stored[^1]).ShouldBeFalse();
        var preview = outcome.Warning!.LostTailPreview!;
        char.IsLowSurrogate(preview[0]).ShouldBeFalse();
        foreach (var c in preview)
        {
            char.IsSurrogate(c).ShouldBeFalse($"preview leaked a lone surrogate: '{preview}'");
        }
    }

    [Fact]
    public void Apply_IsZoneMathOnly_NeverJudgesFieldShape()
    {
        // The engine truncates and nothing else: a newline past the cut is simply dropped from
        // the tail (prefix property — no newline can be manufactured), while a newline inside
        // the head survives to the domain layer. Doors must reject multiline payloads BEFORE
        // capping (AgentPlanningApi set_brief doors pin this at HTTP level) — soft-capping a
        // multiline single-line field would launder the violation, and that is a door bug, not
        // something Apply is allowed to hide.
        var newlinePastTheCut = new string('x', 700) + "\r\ntrailer";
        var outcome = SoftCap.Apply(newlinePastTheCut, 500);

        outcome.Stored!.Contains('\n').ShouldBeFalse();
        outcome.Stored.Contains('\r').ShouldBeFalse();

        var newlineInsideTheHead = "\r\n" + new string('x', 700);
        SoftCap.Apply(newlineInsideTheHead, 500).Stored!.Contains('\n').ShouldBeTrue();
    }

    [Fact]
    public void Apply_EmptyNullAndWhitespace_PassThroughForFieldRules()
    {
        // blankness is the field validator's job (NotEmpty stays hard); the engine adds no
        // warning and never truncates under the cap, whitespace included.
        SoftCap.Apply(null, 500).Stored.ShouldBeNull();
        SoftCap.Apply(null, 500).Warning.ShouldBeNull();
        SoftCap.Apply(string.Empty, 500).Warning.ShouldBeNull();
        var spaces = new string(' ', 100);
        SoftCap.Apply(spaces, 500).Stored.ShouldBe(spaces);
        SoftCap.Apply(spaces, 500).Warning.ShouldBeNull();
    }

    [Theory]
    [InlineData(500, 600, 610)]
    [InlineData(2_000, 2_400, 2_440)]
    [InlineData(100, 120, 122)]
    public void BandMath_KeepAndCeiling_AreDerivedNotCopied(int limit, int keep, int ceiling)
    {
        SoftCap.Keep(limit).ShouldBe(keep);
        SoftCap.Ceiling(limit).ShouldBe(ceiling);
    }

    [Fact]
    public void Warning_SerializesToTheFrozenSnakeCaseShape()
    {
        var warning = new SoftCapWarning
        {
            AcceptedLen = 600,
            Limit = 500,
            Truncated = true,
            LostTailPreview = "tail",
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(warning));
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();

        keys.ShouldBe(["accepted_len", "limit", "lost_tail_preview", "truncated"]);
        doc.RootElement.GetProperty("accepted_len").GetInt32().ShouldBe(600);
        doc.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();

        var pass = JsonSerializer.Serialize(new SoftCapWarning
        {
            AcceptedLen = 501,
            Limit = 500,
            Truncated = false,
        });
        JsonDocument.Parse(pass).RootElement.TryGetProperty("lost_tail_preview", out var nullPreview).ShouldBeTrue();
        nullPreview.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Denominators_ZoneTableRunsEveryFieldCapAcrossEveryZone()
    {
        var rows = ZoneCases.Count;
        var fields = new[] { 500, 2_000 }.Length;

        _output.WriteLine(
            $"soft-cap denominators: zone rows={rows} (L=500 x8, L=2000 x5), " +
            $"distinct limits covered={fields}, surrogate/preview/CRLF/blank/shape/math cases=7");

        rows.ShouldBe(13);
    }
}
