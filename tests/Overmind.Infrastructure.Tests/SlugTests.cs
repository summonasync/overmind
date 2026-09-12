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
    [InlineData("Планировщик Задач", "планировщик-задач")]
    [InlineData("...---...", "fallback")]
    [InlineData("", "fallback")]
    [InlineData("   ", "fallback")]
    public void From_MapsTitlesToSlugs(string value, string expected)
    {
        Slug.From(value, "fallback").ShouldBe(expected);
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
