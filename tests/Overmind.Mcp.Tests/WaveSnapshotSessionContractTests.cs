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
        description.ShouldContain("when coordinating a wave");
        description.ShouldContain("Pick it over planning_task_queue + planning_list_lanes + planning_feature_code");
        description.ShouldContain("compact=true projects every section to coordination-critical fields");

        var properties = tools["overmind_wave_snapshot"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("projectId");
        properties.ShouldContainKey("tags");
        properties.ShouldContainKey("entryLimit");
        properties.ShouldContainKey("fields");
        properties.ShouldContainKey("compact");
        properties["tags"]!["description"]!.GetValue<string>()
            .ShouldContain("default wave-sync,lane-marshalling");
        properties["projectId"]!["description"]!.GetValue<string>().ShouldContain("required");

        var section = ContractsDoc.Section("overmind_wave_snapshot");
        section.ShouldContain("planning_task_queue rows verbatim");
        section.ShouldContain("The git listing is cached briefly");
        section.ShouldContain("a genuine discovery failure degrades to discoveryError");
        section.ShouldContain("projectId is required — the tool errors without it; resolution never creates a project row");
        section.ShouldContain("on each queue row is advisory, never a block");
        section.ShouldContain("literally equal strings");
        section.ShouldContain("sharedDirectory");
        section.ShouldContain("queue rows keep potentialOverlaps verbatim");
    }
}
