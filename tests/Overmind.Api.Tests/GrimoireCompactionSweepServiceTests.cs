using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class GrimoireCompactionSweepServiceTests
{
    [Fact]
    public void IsDue_ZeroHours_NeverRunsTheSweep()
    {
        var now = DateTimeOffset.UtcNow;

        GrimoireCompactionSweepService.IsDue(null, 0, now).ShouldBeFalse();
        GrimoireCompactionSweepService.IsDue(now.AddDays(-30), 0, now).ShouldBeFalse();
    }

    [Fact]
    public void IsDue_FirstTick_RunsImmediately()
    {
        var now = DateTimeOffset.UtcNow;

        GrimoireCompactionSweepService.IsDue(null, 24, now).ShouldBeTrue();
    }

    [Fact]
    public void IsDue_InsideTheCadenceWindow_WaitsForTheNextSlot()
    {
        var lastRun = DateTimeOffset.UtcNow;
        var threeHoursLater = lastRun.AddHours(3);

        GrimoireCompactionSweepService.IsDue(lastRun, 24, threeHoursLater).ShouldBeFalse();
        GrimoireCompactionSweepService.IsDue(lastRun, 24, lastRun.AddHours(24)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, 24)]
    [InlineData("not-a-number", 24)]
    [InlineData("-3", 24)]
    [InlineData("12", 12)]
    [InlineData("0", 0)]
    public void NormalizeHours_BadValuesFallBackToDefault(string? raw, int expected) =>
        GrimoireCompactionSweepService.NormalizeHours(raw, 24).ShouldBe(expected);
}
