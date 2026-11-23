using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class McpProfileResolverTests : IDisposable
{
    public void Dispose() =>
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, null);

    [Fact]
    public void Resolve_NoEnvironmentAndNoArgument_FallsBackToPlanning()
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, null);

        var resolved = McpProfileResolver.TryResolve([], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Planning);
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("journal")]
    [InlineData("Journal")]
    [InlineData(" JOURNAL ")]
    public void Resolve_EnvironmentRequestsJournal_YieldsJournal(string raw)
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, raw);

        var resolved = McpProfileResolver.TryResolve([], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Journal);
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("planning")]
    [InlineData("PLANNING")]
    public void Resolve_EnvironmentRequestsPlanning_YieldsPlanning(string raw)
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, raw);

        var resolved = McpProfileResolver.TryResolve([], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Planning);
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankEnvironment_FallsThroughToArguments(string? raw)
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, raw);

        var resolved = McpProfileResolver.TryResolve(["--profile", "journal"], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Journal);
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("journalx")]
    [InlineData("plannig")]
    public void Resolve_UnknownEnvironmentValue_RefusesWithNamedErrorAndFixHint(string raw)
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, raw);

        var resolved = McpProfileResolver.TryResolve([], out var profile, out var error);

        resolved.ShouldBeFalse();
        profile.ShouldBe(McpProfile.Planning);
        error.ShouldNotBeNull();
        error.ShouldContain("UnknownMcpProfile");
        error.ShouldContain(raw);
        error.ShouldContain(McpProfileResolver.EnvironmentVariable);
        error.ShouldContain("--profile");
        error.ShouldContain("planning, journal");
    }

    [Fact]
    public void Resolve_ArgumentWithoutValue_IsIgnoredAndDefaultsToPlanning()
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, null);

        var resolved = McpProfileResolver.TryResolve(["--profile"], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Planning);
        error.ShouldBeNull();
    }

    [Fact]
    public void Resolve_ArgumentAssignForm_IsHonored()
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, null);

        var resolved = McpProfileResolver.TryResolve(["--profile=journal"], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Journal);
        error.ShouldBeNull();
    }

    [Fact]
    public void Resolve_EnvironmentBeatsArgument()
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, "planning");

        var resolved = McpProfileResolver.TryResolve(["--profile", "journal"], out var profile, out var error);

        resolved.ShouldBeTrue();
        profile.ShouldBe(McpProfile.Planning);
        error.ShouldBeNull();
    }

    [Fact]
    public void Resolve_UnknownArgumentValue_RefusesWithNamedError()
    {
        Environment.SetEnvironmentVariable(McpProfileResolver.EnvironmentVariable, null);

        var resolved = McpProfileResolver.TryResolve(["--profile", "wat"], out var profile, out var error);

        resolved.ShouldBeFalse();
        error.ShouldNotBeNull();
        error.ShouldContain("UnknownMcpProfile");
        error.ShouldContain("'wat'");
        error.ShouldContain("--profile argument");
    }

    [Theory]
    [InlineData(McpProfile.Planning, "planning")]
    [InlineData(McpProfile.Journal, "journal")]
    public void Describe_MatchesTheCanonicalProfileNames(McpProfile profile, string expected) =>
        McpProfileResolver.Describe(profile).ShouldBe(expected);
}
