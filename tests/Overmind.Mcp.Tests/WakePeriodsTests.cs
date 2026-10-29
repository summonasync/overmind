using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(AdapterSessionState))]
public sealed class WakePeriodsTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(WakePeriods.IdleEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(WakePeriods.GateEnvironmentVariable, null);
    }

    [Fact]
    public void Resolve_Unset_ReturnsDefaults()
    {
        WakePeriods.ResolveIdle().ShouldBe(TimeSpan.FromSeconds(WakePeriods.DefaultIdleSeconds));
        WakePeriods.ResolveGate().ShouldBe(TimeSpan.FromSeconds(WakePeriods.DefaultGateSeconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("junk")]
    [InlineData("2.5")]
    public void Resolve_UnparsableIdle_ReturnsDefaultIdle(string? raw)
    {
        Environment.SetEnvironmentVariable(WakePeriods.IdleEnvironmentVariable, raw);

        WakePeriods.ResolveIdle().ShouldBe(TimeSpan.FromSeconds(WakePeriods.DefaultIdleSeconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("junk")]
    [InlineData("1.5")]
    public void Resolve_UnparsableGate_ReturnsDefaultGate(string? raw)
    {
        Environment.SetEnvironmentVariable(WakePeriods.GateEnvironmentVariable, raw);

        WakePeriods.ResolveGate().ShouldBe(TimeSpan.FromSeconds(WakePeriods.DefaultGateSeconds));
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("45", 45)]
    [InlineData("600", 600)]
    [InlineData(" 7 ", 7)]
    public void Resolve_ValidIdleSeconds_IsHonored(string raw, int seconds)
    {
        Environment.SetEnvironmentVariable(WakePeriods.IdleEnvironmentVariable, raw);

        WakePeriods.ResolveIdle().ShouldBe(TimeSpan.FromSeconds(seconds));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("12", 12)]
    [InlineData(" 9 ", 9)]
    public void Resolve_ValidGateSeconds_IsHonored(string raw, int seconds)
    {
        Environment.SetEnvironmentVariable(WakePeriods.GateEnvironmentVariable, raw);

        WakePeriods.ResolveGate().ShouldBe(TimeSpan.FromSeconds(seconds));
    }

    [Theory]
    [InlineData(-9, 5)]
    [InlineData(0, 5)]
    [InlineData(5, 5)]
    [InlineData(90, 90)]
    [InlineData(601, 600)]
    public void FromSeconds_IdleOutsideBounds_IsClamped(int raw, int expected)
    {
        Environment.SetEnvironmentVariable(WakePeriods.IdleEnvironmentVariable, raw.ToString());

        WakePeriods.FromSeconds(
                WakePeriods.IdleEnvironmentVariable,
                WakePeriods.DefaultIdleSeconds,
                WakePeriods.MinIdleSeconds,
                WakePeriods.MaxIdleSeconds)
            .ShouldBe(TimeSpan.FromSeconds(expected));
    }

    [Theory]
    [InlineData(-9, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(61, 60)]
    public void FromSeconds_GateOutsideBounds_IsClamped(int raw, int expected)
    {
        Environment.SetEnvironmentVariable(WakePeriods.GateEnvironmentVariable, raw.ToString());

        WakePeriods.FromSeconds(
                WakePeriods.GateEnvironmentVariable,
                WakePeriods.DefaultGateSeconds,
                WakePeriods.MinGateSeconds,
                WakePeriods.MaxGateSeconds)
            .ShouldBe(TimeSpan.FromSeconds(expected));
    }
}
