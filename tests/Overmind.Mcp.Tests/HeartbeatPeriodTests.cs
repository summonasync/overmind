using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class HeartbeatPeriodTests : IDisposable
{
    public void Dispose() =>
        Environment.SetEnvironmentVariable(HeartbeatPeriod.EnvironmentVariable, null);

    [Fact]
    public void Resolve_Unset_ReturnsDefault()
    {
        Environment.SetEnvironmentVariable(HeartbeatPeriod.EnvironmentVariable, null);

        HeartbeatPeriod.Resolve().ShouldBe(TimeSpan.FromMinutes(HeartbeatPeriod.DefaultMinutes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("junk")]
    [InlineData("2.5")]
    public void Resolve_Unparsable_ReturnsDefault(string? raw)
    {
        Environment.SetEnvironmentVariable(HeartbeatPeriod.EnvironmentVariable, raw);

        HeartbeatPeriod.Resolve().ShouldBe(TimeSpan.FromMinutes(HeartbeatPeriod.DefaultMinutes));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("17", 17)]
    [InlineData("30", 30)]
    [InlineData(" 7 ", 7)]
    public void Resolve_ValidMinutes_IsHonored(string raw, int minutes)
    {
        Environment.SetEnvironmentVariable(HeartbeatPeriod.EnvironmentVariable, raw);

        HeartbeatPeriod.Resolve().ShouldBe(TimeSpan.FromMinutes(minutes));
    }

    [Theory]
    [InlineData(-9, 1)]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(30, 30)]
    [InlineData(90, 30)]
    public void FromMinutes_OutsideBounds_IsClamped(int raw, int expected) =>
        HeartbeatPeriod.FromMinutes(raw).ShouldBe(TimeSpan.FromMinutes(expected));
}
