using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class McpPlanningProfileStartupContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public void Stderr_NamesTheDefaultPlanningProfile()
    {
        Driver.Stderr.ShouldContain("[overmind-mcp] MCP profile: planning");
    }

    [Fact]
    public async Task Tools_WithoutProfileConfiguration_ListThePlanningSurface()
    {
        var toolNames = (await Driver.ListToolsAsync())["result"]!["tools"]!.AsArray()
            .Select(t => t!["name"]!.GetValue<string>())
            .ToList();

        toolNames.ShouldNotBeEmpty();
        toolNames.ShouldContain("planning_claim_feature");
        toolNames.ShouldContain("overmind_onboard");
    }
}
