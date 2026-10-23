using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class BacklogNamingTests
{
    [Theory]
    [InlineData("Feature 010 — Demo feature title", "Feature", "010", "Demo feature title")]
    [InlineData("Feature 010: Demo feature title", "Feature", "010", "Demo feature title")]
    [InlineData("Feature 010 — Feature 010: Demo feature title", "Feature", "010", "Demo feature title")]
    [InlineData("Feature 10 — Plain number", "Feature", "10", "Plain number")]
    [InlineData("Feature 010: Feature 010 — Demo feature title", "Feature", "010", "Demo feature title")]
    [InlineData("Task 01 — First task", "Task", "01", "First task")]
    [InlineData("Task 01: First task", "Task", "01", "First task")]
    [InlineData("Task 01 — Task 01: First task", "Task", "01", "First task")]
    [InlineData("Feature 010 — Untitled: keep the rest", "Feature", "010", "Untitled: keep the rest")]
    [InlineData("Unrelated heading", "Feature", "010", "Unrelated heading")]
    public void StripTitlePrefix_MatchingForm_StripsOnlyThePrefix(
        string heading, string kind, string number, string expected)
    {
        BacklogNaming.StripTitlePrefix(heading, kind, number).ShouldBe(expected);
    }

    [Fact]
    public void StripTitlePrefix_NullHeading_ReturnsNull()
    {
        BacklogNaming.StripTitlePrefix(null, "Feature", "010").ShouldBeNull();
    }

    [Fact]
    public void StripTitlePrefix_BareSeparatorWithoutTitle_ReturnsHeadingUnchanged()
    {
        BacklogNaming.StripTitlePrefix("Feature 010 —", "Feature", "010").ShouldBe("Feature 010 —");
    }

    [Fact]
    public void MirrorSlug_AsciiTitleWithoutLoss_KeepsPlainSlug()
    {
        BacklogNaming.MirrorSlug("Bounded Mirror Names", "fallback").ShouldBe("bounded-mirror-names");
        BacklogNaming.MirrorSlug("Data + EF: blockedBy", "fallback").ShouldBe("data-ef-blockedby");
    }

    [Fact]
    public void MirrorSlug_CyrillicTitle_DropsTransliterationAndMarksTheLoss()
    {
        var slug = BacklogNaming.MirrorSlug("Материализация зеркал", "task-15");

        slug.ShouldBe("task-15-e0f1e3b0");
    }

    [Fact]
    public void MirrorSlug_EmojiTitle_KeepsAsciiCoreAndMarksTheLoss()
    {
        BacklogNaming.MirrorSlug("🚀 Launch 🚀", "fallback").ShouldBe("launch-fd57b9d5");
    }

    [Theory]
    [InlineData("Kelvin K units")]
    [InlineData("İstanbul node")]
    [InlineData("ſharp long s")]
    public void MirrorSlug_NonAsciiTitleChars_MarkTheLossDeterministically(string title)
    {
        var slug = BacklogNaming.MirrorSlug(title, "fallback");

        slug.Length.ShouldBeGreaterThan(9);
        slug[^9].ShouldBe('-');
        slug[^8..].All(char.IsAsciiHexDigit).ShouldBeTrue();
        slug.ShouldBe(BacklogNaming.MirrorSlug(title, "fallback"));
    }

    [Fact]
    public void MirrorSlug_EmptySlug_FallsBackWithMarker()
    {
        BacklogNaming.MirrorSlug("   ", "entry").ShouldBe("entry");
        BacklogNaming.MirrorSlug("Решение по кэшированию", "entry").ShouldBe("entry-75f09372");
    }

    [Fact]
    public void MirrorSlug_LongAsciiTitle_TruncatesToBudgetAndMarksTheLoss()
    {
        var slug = BacklogNaming.MirrorSlug(new string('a', 49), "fallback");

        slug.ShouldStartWith(new string('a', 48));
        slug.ShouldNotBe(new string('a', 49));
        slug.Length.ShouldBe(57);
    }

    [Fact]
    public void MirrorSlug_TitleAtBudgetBoundary_StaysUnmarked()
    {
        BacklogNaming.MirrorSlug(new string('b', 48), "fallback").ShouldBe(new string('b', 48));
    }

    [Fact]
    public void MirrorSlug_RepeatedCalls_AreDeterministic()
    {
        var titles = new[] { "Материализация зеркал", "🚀 Launch", new string('a', 300), "Plain ASCII" };

        foreach (var title in titles)
        {
            BacklogNaming.MirrorSlug(title, "fallback").ShouldBe(BacklogNaming.MirrorSlug(title, "fallback"));
        }
    }

    [Theory]
    [InlineData("Материализация зеркал")]
    [InlineData("🚀 Launch 🚀 кипарис")]
    [InlineData("Sync_LongTitle_" + "x")]
    public void TaskFileName_NastyTitles_ProduceBoundedAsciiParseableNames(string title)
    {
        var longTitle = title.Length > 100 ? title : title + new string('й', 300);
        var fileName = BacklogNaming.TaskFileName(15, BacklogNaming.MirrorSlug(longTitle, "task-15"));

        fileName.Length.ShouldBeLessThanOrEqualTo(96);
        fileName.All(char.IsAscii).ShouldBeTrue();
        BacklogNaming.TryParseTaskOrder(fileName, out var order, out _).ShouldBeTrue();
        order.ShouldBe(15);
        BacklogNaming.KeepsTaskFileName(fileName, 15, BacklogNaming.MirrorSlug(longTitle, "task-15"))
            .ShouldBeTrue();
    }

    [Fact]
    public void KeepsTaskFileName_ImportedAsciiName_SurvivesALosslessFreshSlug()
    {
        BacklogNaming.KeepsTaskFileName("01-first.md", 1, "task-one").ShouldBeTrue();
        BacklogNaming.KeepsTaskFileName("02-task-02.md", 2, "task-02").ShouldBeTrue();
    }

    [Fact]
    public void KeepsTaskFileName_LegacyTranslitNameWithMarkedFreshSlug_Converges()
    {
        BacklogNaming.KeepsTaskFileName(
            "02-cpm-obnovlenie-paketov-aspire-13-5.md", 2, "cpm-aspire-13-5-c25cc171").ShouldBeFalse();
    }

    [Fact]
    public void KeepsTaskFileName_CurrentFormatName_MatchesItsOwnFreshSlug()
    {
        var fresh = "cpm-aspire-13-5-c25cc171";
        BacklogNaming.KeepsTaskFileName($"02-{fresh}.md", 2, fresh).ShouldBeTrue();
    }

    [Fact]
    public void KeepsTaskFileName_StaleMarkerOrForeignOrder_Renames()
    {
        BacklogNaming.KeepsTaskFileName("02-cpm-aspire-13-5-c25cc171.md", 2, "cpm-aspire-13-5-99aa99aa")
            .ShouldBeFalse();
        BacklogNaming.KeepsTaskFileName("02-first.md", 3, "task-one").ShouldBeFalse();
        BacklogNaming.KeepsTaskFileName("02-решение.md", 2, "task-02").ShouldBeFalse();
    }

    [Fact]
    public void KeepsTaskFileName_OverBudgetAsciiSlug_Renames()
    {
        var overBudget = new string('a', 60);
        BacklogNaming.KeepsTaskFileName($"01-{overBudget}.md", 1, "task-one").ShouldBeFalse();
    }

    [Fact]
    public void KeepsEntryFileName_LegacyNames_ConvergeCurrentNamesSurvive()
    {
        var id8 = "a1b2c3d4";

        BacklogNaming.KeepsEntryFileName("решение-по-кэшированию-a1b2c3d4.md", "entry-75f09372", id8)
            .ShouldBeFalse();
        BacklogNaming.KeepsEntryFileName("reshenie-po-keshirovaniyu-a1b2c3d4.md", "entry-75f09372", id8)
            .ShouldBeFalse();
        BacklogNaming.KeepsEntryFileName($"old-title-{id8}.md", "new-title", id8).ShouldBeTrue();
        BacklogNaming.KeepsEntryFileName($"entry-75f09372-{id8}.md", "entry-75f09372", id8).ShouldBeTrue();
        BacklogNaming.KeepsEntryFileName($"cache-decision-{id8}.md", "cache-decision", id8).ShouldBeTrue();
        BacklogNaming.KeepsEntryFileName($"other-id-9988ccdd.md", "entry", id8).ShouldBeFalse();
    }
}
