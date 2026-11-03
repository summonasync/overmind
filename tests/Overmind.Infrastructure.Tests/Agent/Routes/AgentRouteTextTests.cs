using Overmind.Infrastructure.Agent.Routes;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent.Routes;

public sealed class AgentRouteTextTests
{
    [Theory]
    [InlineData("  Sort   THE  board!!  ", "sort the board")]
    [InlineData("Как дела, друг?", "как дела, друг")]
    [InlineData("read\tthe\nwave   rows", "read the wave rows")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_CollapsesCaseWhitespaceAndSymbols(string raw, string expected) =>
        AgentRouteText.Normalize(raw).ShouldBe(expected);

    [Fact]
    public void Normalize_TruncatesToThePatternCap()
    {
        var normalized = AgentRouteText.Normalize(new string('a', 300));

        normalized.Length.ShouldBe(AgentRouteText.MaxPatternLength);
    }

    [Theory]
    [InlineData("sort the board", "sort board", true)]
    [InlineData("sort the board", "sort, board, planning", true)]
    [InlineData("facts from grimoire memory", "grimoire; memory", true)]
    [InlineData("sort the board", "planning", false)]
    [InlineData("board", "sort board", false)]
    [InlineData("Sort the BOARD!", "sort board", true)]
    public void Matches_SplitsPhrasesFromKeywordLists(string text, string pattern, bool expected) =>
        AgentRouteText.Matches(text, pattern).ShouldBe(expected);

    [Theory]
    [InlineData("", "sort")]
    [InlineData("sort the board", "")]
    [InlineData("   ", ",,,")]
    public void Matches_EmptyInputsNeverMatch(string text, string pattern) =>
        AgentRouteText.Matches(text, pattern).ShouldBeFalse();

    [Fact]
    public void Clamp_KeepsTheShortReasonWithinItsCap()
    {
        var clamped = AgentRouteText.Clamp(new string('b', 200), AgentRouteText.MaxMissReasonLength);

        clamped.Length.ShouldBe(AgentRouteText.MaxMissReasonLength);
    }
}
