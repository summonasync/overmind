using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallOptionsParserTests
{
    [Fact]
    public void EmptyArgs_AllDefaults()
    {
        var parsed = InstallOptionsParser.TryParse([], out var options, out var error);

        parsed.ShouldBeTrue();
        error.ShouldBeNull();
        options.ShouldNotBeNull();
        options!.All.ShouldBeFalse();
        options.Agents.ShouldBeEmpty();
        options.Project.ShouldBeFalse();
        options.Remove.ShouldBeFalse();
        options.DryRun.ShouldBeFalse();
    }

    [Fact]
    public void AllFlags_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(
            ["--all", "--project", "--remove", "--dry-run"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.All.ShouldBeTrue();
        options.Project.ShouldBeTrue();
        options.Remove.ShouldBeTrue();
        options.DryRun.ShouldBeTrue();
    }

    [Fact]
    public void OnboardFlag_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--onboard"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Onboard.ShouldBeTrue();
    }

    [Fact]
    public void OnboardWithRemove_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--onboard", "--remove"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--onboard");
        error.ShouldContain("--remove");
    }

    [Fact]
    public void AgentsCsv_TrimmedDedupedPreservingOrder()
    {
        var parsed = InstallOptionsParser.TryParse(["--agents", " claude, kimi-code ,CLAUDE"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Agents.ShouldBe(["claude", "kimi-code"]);
    }

    [Fact]
    public void AgentsAsLastTokenWithoutValue_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--dry-run", "--agents"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--agents");
    }

    [Fact]
    public void AgentsFollowedByAnotherFlag_FailsWithoutConsumingIt()
    {
        var parsed = InstallOptionsParser.TryParse(["--agents", "--dry-run"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--agents");
    }

    [Fact]
    public void AgentsEmptyValue_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--agents", " , "], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--agents");
    }

    [Fact]
    public void UnknownAgentName_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--agents", "kimi-code,unknown-agent"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("unknown-agent");
    }

    [Fact]
    public void UnknownFlag_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--bogus"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--bogus");
    }

    [Fact]
    public void AgentsCombinedWithAll_BothRecordedAgentsWin()
    {
        var parsed = InstallOptionsParser.TryParse(["--all", "--agents", "codex"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.All.ShouldBeTrue();
        options.Agents.ShouldBe(["codex"]);
    }

    [Fact]
    public void UsageText_MentionsEveryFlag()
    {
        foreach (var flag in new[] { "--all", "--agents", "--project", "--onboard", "--remove", "--dry-run" })
        {
            InstallOptionsParser.UsageText.ShouldContain(flag);
        }
    }
}
