using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class InstallOptionsParserGrimoireTests
{
    [Fact]
    public void GrimoireFlagsDefault_False()
    {
        var parsed = InstallOptionsParser.TryParse([], out var options, out _);

        parsed.ShouldBeTrue();
        options!.WithGrimoire.ShouldBeFalse();
        options.RemoveGrimoire.ShouldBeFalse();
    }

    [Fact]
    public void WithGrimoireFlag_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--all", "--with-grimoire"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.All.ShouldBeTrue();
        options.WithGrimoire.ShouldBeTrue();
        options.RemoveGrimoire.ShouldBeFalse();
        options.Remove.ShouldBeFalse();
    }

    [Fact]
    public void RemoveGrimoireFlag_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--agents", "claude", "--remove-grimoire", "--dry-run"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Agents.ShouldBe(["claude"]);
        options.RemoveGrimoire.ShouldBeTrue();
        options.WithGrimoire.ShouldBeFalse();
        options.DryRun.ShouldBeTrue();
    }

    [Fact]
    public void WithGrimoireWithRemoveGrimoire_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--with-grimoire", "--remove-grimoire"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--with-grimoire");
        error.ShouldContain("--remove-grimoire");
    }

    [Fact]
    public void WithGrimoireWithRemove_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--with-grimoire", "--remove"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--with-grimoire");
        error.ShouldContain("--remove");
    }

    [Fact]
    public void RemoveGrimoireWithRemove_Fails()
    {
        var parsed = InstallOptionsParser.TryParse(["--remove", "--remove-grimoire"], out _, out var error);

        parsed.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("--remove-grimoire");
        error.ShouldContain("--remove");
    }

    [Fact]
    public void OnboardWithRemoveGrimoire_Parsed()
    {
        var parsed = InstallOptionsParser.TryParse(["--onboard", "--remove-grimoire"], out var options, out _);

        parsed.ShouldBeTrue();
        options!.Onboard.ShouldBeTrue();
        options.RemoveGrimoire.ShouldBeTrue();
    }

    [Fact]
    public void UsageText_MentionsGrimoireFlags()
    {
        InstallOptionsParser.UsageText.ShouldContain("--with-grimoire");
        InstallOptionsParser.UsageText.ShouldContain("--remove-grimoire");
    }
}
