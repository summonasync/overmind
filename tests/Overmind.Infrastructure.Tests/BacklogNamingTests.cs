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
}
