using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class ProcStatParserTests
{
    [Theory]
    [InlineData("12249 (cat) R 2405 12249 2405 34816 12249 0 ...", 2405)]
    [InlineData("999 (cu:te (agent) x) S 77 999 77 1024 999 0 ...", 77)]
    [InlineData("2 (kthreadd) S 1 0 0 0 -1 ...", 1)]
    public void TypicalStatLine_ReturnsTheParentProcessId(string statLine, int expected)
    {
        ProcStatParser.ParentProcessIdOf(statLine).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no comm at all 123")]
    [InlineData("1234 (agent) S")]
    [InlineData("1234 (agent) S not-a-number")]
    [InlineData("1234 (agent) S 0")]
    [InlineData("1234 (agent) S -5")]
    public void MalformedStatLine_ReturnsNull(string statLine)
    {
        ProcStatParser.ParentProcessIdOf(statLine).ShouldBeNull();
    }
}
