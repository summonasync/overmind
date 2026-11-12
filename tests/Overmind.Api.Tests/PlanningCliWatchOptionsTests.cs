using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningCliWatchOptionsTests
{
    [Fact]
    public void From_WithNoFlags_AppliesDefaultsAndNoKindFilter()
    {
        var args = PlanningCliArgs.Parse(["planning", "watch"]);

        var options = PlanningCliWatchOptions.From(args);

        options.Alias.ShouldBeNull();
        options.KindFilter.ShouldBeNull();
        options.Timeout.ShouldBe(TimeSpan.FromMinutes(30));
        options.Interval.ShouldBe(TimeSpan.FromSeconds(10));
        args.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void From_WithAliasAndDurations_ParsesAllValues()
    {
        var args = PlanningCliArgs.Parse(
            ["planning", "watch", "--alias", "billy", "--timeout", "5m", "--interval", "90s"]);

        var options = PlanningCliWatchOptions.From(args);

        options.Alias.ShouldBe("billy");
        options.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.Interval.ShouldBe(TimeSpan.FromSeconds(90));
        args.Problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--mentions", "wave-mention")]
    [InlineData("--replies", "notification-reply")]
    public void From_WithSingleKindFlag_MapsTheKind(string flag, string kind)
    {
        var args = PlanningCliArgs.Parse(["planning", "watch", flag]);

        var options = PlanningCliWatchOptions.From(args);

        options.KindFilter.ShouldBe(kind);
        args.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void From_WithBothKindFlags_NamesTheExclusiveContract()
    {
        var args = PlanningCliArgs.Parse(["planning", "watch", "--mentions", "--replies"]);

        var options = PlanningCliWatchOptions.From(args);

        options.KindFilter.ShouldBeNull();
        args.Problems.Single().ShouldContain("--mentions and --replies are exclusive");
    }

    [Theory]
    [InlineData("90s", 0, 0, 0, 90)]
    [InlineData("30m", 0, 0, 30, 0)]
    [InlineData("2h", 0, 2, 0, 0)]
    [InlineData("1d", 1, 0, 0, 0)]
    [InlineData("1.5h", 0, 1, 30, 0)]
    [InlineData("00:30:00", 0, 0, 30, 0)]
    [InlineData("1.02:00:00", 1, 2, 0, 0)]
    public void TryParseDuration_AcceptsUnitAndClockForms(
        string raw, int days, int hours, int minutes, int seconds)
    {
        var parsed = PlanningCliWatchOptions.TryParseDuration(raw, out var value);

        parsed.ShouldBeTrue();
        value.ShouldBe(new TimeSpan(days, hours, minutes, seconds));
    }

    [Theory]
    [InlineData("30")]
    [InlineData("abc")]
    [InlineData("-5m")]
    [InlineData("")]
    public void TryParseDuration_RejectsUnitlessAndGarbageInput(string raw)
    {
        var parsed = PlanningCliWatchOptions.TryParseDuration(raw, out var value);

        parsed.ShouldBeFalse();
        value.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void From_WithUnparsableTimeout_NamesTheContract()
    {
        var args = PlanningCliArgs.Parse(["planning", "watch", "--timeout", "abc"]);

        var options = PlanningCliWatchOptions.From(args);

        options.Timeout.ShouldBe(TimeSpan.Zero);
        args.Problems.Single().ShouldContain("--timeout expects a duration above zero");
    }

    [Fact]
    public void From_WithUnitlessInterval_NamesTheContract()
    {
        var args = PlanningCliArgs.Parse(["planning", "watch", "--interval", "30"]);

        var options = PlanningCliWatchOptions.From(args);

        args.Problems.Single().ShouldContain("--interval expects a duration above zero");
    }

    [Fact]
    public void From_WithZeroInterval_NamesTheContract()
    {
        var args = PlanningCliArgs.Parse(["planning", "watch", "--interval", "0s"]);

        var options = PlanningCliWatchOptions.From(args);

        args.Problems.Single().ShouldContain("--interval expects a duration above zero");
    }
}
