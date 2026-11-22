using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class McpUnknownProfileRefusalContractTests : McpSessionTestBase
{
    protected override bool AutoHandshake => false;

    protected override IReadOnlyDictionary<string, string>? EnvironmentOverrides =>
        new Dictionary<string, string> { [McpProfileResolver.EnvironmentVariable] = "bogus" };

    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task Startup_UnknownProfileValue_RefusesBeforeAnyHostConnection()
    {
        var exitCode = await Driver.WaitForExitAsync(TimeSpan.FromSeconds(30));

        exitCode.ShouldBe(2, $"expected the refusal exit code, stderr was: {Driver.Stderr}");
        Driver.Stderr.ShouldContain("UnknownMcpProfile");
        Driver.Stderr.ShouldContain("'bogus'");
        Driver.Stderr.ShouldContain("planning, journal");
        Driver.Stderr.ShouldContain("Fix:");

        Stub.Requests.ShouldBeEmpty("the profile refusal must land before any host connection is made");
    }
}
