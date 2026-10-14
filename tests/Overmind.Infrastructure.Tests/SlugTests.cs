using System.Globalization;
using Overmind.Infrastructure.Materialization;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class SlugTests
{
    [Theory]
    [InlineData("Reviewer", "reviewer")]
    [InlineData("Code Reviewer", "code-reviewer")]
    [InlineData("a,b", "a-b")]
    [InlineData("a--b  c", "a-b-c")]
    [InlineData("C# Guide", "c-guide")]
    [InlineData("  --Weird  Title--  ", "weird-title")]
    [InlineData("# 006/05 — Planning materialization", "006-05-planning-materialization")]
    [InlineData("Планировщик Задач", "planirovshchik-zadach")]
    [InlineData("Реализация React-хуков", "realizatsiya-react-khukov")]
    [InlineData("...---...", "fallback")]
    [InlineData("", "fallback")]
    [InlineData("   ", "fallback")]
    public void From_MapsTitlesToSlugs(string value, string expected)
    {
        Slug.From(value, "fallback").ShouldBe(expected);
    }

    [Theory]
    [InlineData('ж', "zh")]
    [InlineData('х', "kh")]
    [InlineData('ц', "ts")]
    [InlineData('ч', "ch")]
    [InlineData('ш', "sh")]
    [InlineData('щ', "shch")]
    [InlineData('ы', "y")]
    [InlineData('э', "e")]
    [InlineData('ю', "yu")]
    [InlineData('я', "ya")]
    [InlineData('ё', "yo")]
    [InlineData('й', "y")]
    public void From_HardCyrillicLetters_TransliteratesPerIso9AsciiTable(char letter, string expected)
    {
        Slug.From(letter.ToString(), "fallback").ShouldBe(expected);
    }

    [Theory]
    [InlineData("ъ", "fallback")]
    [InlineData("ь", "fallback")]
    [InlineData("подъезд", "podezd")]
    [InlineData("роль", "rol")]
    public void From_CyrillicSigns_VanishWithoutCreatingDashes(string value, string expected)
    {
        Slug.From(value, "fallback").ShouldBe(expected);
    }

    [Fact]
    public void From_TransliteratedSlug_IsIdempotent()
    {
        var slug = Slug.From("Щелчок Мыши", "fallback");

        slug.ShouldBe("shchelchok-myshi");
        Slug.From(slug, "fallback").ShouldBe(slug);
    }

    [Theory]
    [InlineData("Отчёт 😀 готов", "otchyot-gotov")]
    [InlineData("😀😀", "fallback")]
    [InlineData("日本語", "fallback")]
    public void From_NonCyrillicNonAscii_StripsAndFallsBackOnEmptyResult(string value, string expected)
    {
        Slug.From(value, "fallback").ShouldBe(expected);
    }

    [Fact]
    public void From_IgnoresCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Slug.From("ЖЁСТКИЙ Title", "fallback").ShouldBe("zhyostkiy-title");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void From_NeverProducesPathSegmentsOrEmptyOutput()
    {
        var scary = new[]
        {
            "../evil", "..\\evil", "a/b", "a\\b", "..", "...", "———", "😀😀",
            "task: 05", "# Heading!", "\t\n\r", "a.b.c",
        };

        foreach (var value in scary)
        {
            var slug = Slug.From(value, "fallback");

            slug.ShouldNotBeNullOrEmpty();
            slug.ShouldNotBe("..");
            slug.ShouldNotContain('/');
            slug.ShouldNotContain('\\');
            slug.ShouldNotContain("..");
            slug.StartsWith('-').ShouldBeFalse();
            slug.EndsWith('-').ShouldBeFalse();
        }
    }
}
