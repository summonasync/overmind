using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class WaveSnapshotSessionContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task WaveSnapshot_MissingProjectId_IsAToolErrorNamingTheGuard()
    {
        var response = await Driver.CallToolAsync("overmind_wave_snapshot", new JsonObject());

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("projectId is required for this tool");
    }

    [Fact]
    public async Task WaveSnapshot_DescriptionAndSchema_PinTheWaveContract()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_wave_snapshot"]!["description"]!.GetValue<string>();

        description.ShouldContain("wave coordination");
        description.ShouldContain("planning_task_queue rows");
        description.ShouldContain("wave-sync");
        description.ShouldContain("lane-marshalling");
        description.ShouldContain("discoveryError");
        description.ShouldContain("cached");
        description.ShouldContain("projectId is required");
        description.ShouldContain("advisory projection, never a block");
        description.ShouldContain("literally equal");
        description.ShouldContain("sharedDirectory");
        description.ShouldContain("potentialOverlaps verbatim");

        var properties = tools["overmind_wave_snapshot"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("projectId");
        properties.ShouldContainKey("tags");
        properties.ShouldContainKey("entryLimit");
        properties.ShouldContainKey("fields");
        properties.ShouldContainKey("compact");
    }
}
