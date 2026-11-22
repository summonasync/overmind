using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallOptionsParserProfileTests
{
    [Fact]
    public void NoFlag_DefaultsToPlanning()
    {
        var parsed = InstallOptionsParser.TryParse(["--all"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Profile.ShouldBe(McpProfile.Planning);
    }

    [Fact]
    public void JournalFlag_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--profile", "journal"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Profile.ShouldBe(McpProfile.Journal);
    }

    [Fact]
    public void PlanningFlag_Explicit_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--profile", "planning"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Profile.ShouldBe(McpProfile.Planning);
    }

    [Fact]
    public void EqualsForm_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--profile=journal", "--all"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Profile.ShouldBe(McpProfile.Journal);
        options.All.ShouldBeTrue();
    }

    [Fact]
    public void ValueCaseInsensitive_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--profile", "Journal"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Profile.ShouldBe(McpProfile.Journal);
    }

    [Fact]
    public void UnknownValue_RefusesNamingValueAndValidProfiles()
    {
        var parsed = InstallOptionsParser.TryParse(["--profile", "both"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("unknown profile 'both'");
        error.ShouldContain("planning");
        error.ShouldContain("journal");
    }

    [Fact]
    public void MissingValue_Refuses()
    {
        var parsed = InstallOptionsParser.TryParse(["--dry-run", "--profile"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--profile");
    }

    [Fact]
    public void ValueBeforeAnotherFlag_RefusesWithoutConsumingIt()
    {
        var parsed = InstallOptionsParser.TryParse(["--profile", "--all"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--profile");
    }

    [Fact]
    public void UsageText_CoversFlagAndPinnedEnvVar()
    {
        InstallOptionsParser.UsageText.ShouldContain("--profile");
        InstallOptionsParser.UsageText.ShouldContain("journal");
        InstallOptionsParser.UsageText.ShouldContain("planning");
        InstallOptionsParser.UsageText.ShouldContain("OVERMIND_MCP_PROFILE=journal");
        InstallOptionsParser.UsageText.ShouldContain("overmind-journal");
    }
}
