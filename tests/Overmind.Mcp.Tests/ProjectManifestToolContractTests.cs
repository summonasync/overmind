using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ProjectManifestToolContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub) => RegisterDefaultRoutes(stub);

    [Fact]
    public async Task ReadProjectManifest_ReturnsTheMarkdownVerbatim()
    {
        Stub.RouteGet($"/api/projects/{Canned.ProjectId}", Canned.ProjectWithManifestJson);

        var response = await Driver.CallToolAsync("read_project_manifest", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldBe(Canned.ProjectManifestText);
    }

    [Fact]
    public async Task ReadProjectManifest_NotSet_SaysSoInsteadOfFailing()
    {
        var response = await Driver.CallToolAsync("read_project_manifest", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("No manifest is set");
        text.ShouldContain("AGENTS.md");
    }

    [Fact]
    public async Task ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd()
    {
        await Driver.CallToolAsync("read_project_manifest", new JsonObject());

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(onboard.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["create"]!.GetValue<bool>().ShouldBeFalse();
        FindRequest("GET", $"/api/projects/{Canned.ProjectId}");
    }

    [Fact]
    public async Task ReadProjectManifest_UnregisteredCwd_IsErrorWithHostGuidanceAndNoRetry()
    {
        Stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);

        var response = await Driver.CallToolAsync("read_project_manifest", new JsonObject());

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("No project is registered for the working directory");
        text.ShouldContain("overmind_onboard");
        Stub.Requests.Count(r => r.Method == "POST" && r.Path == "/api/onboard").ShouldBe(1);
        Stub.Requests.ShouldNotContain(r => r.Path.StartsWith("/api/projects/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadProjectManifest_Description_PinsDeepReadAndResolution()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var description = tools["read_project_manifest"]!["description"]!.GetValue<string>();
        description.ShouldContain("the deep read behind");
        description.ShouldContain("list_projects");
        description.ShouldContain("Says so instead of failing when unset");

        var section = ContractsDoc.Section("read_project_manifest");
        section.ShouldContain("Returns the manifest markdown verbatim");
        section.ShouldContain("import it from AGENTS.md/CLAUDE.md/QWEN.md");
        // Resolution is create:false and an unregistered cwd answers with host guidance; both are
        // pinned as behavior above (ReadProjectManifest_NoExplicitProject_ResolvesThroughTheOnboardCwd,
        // ReadProjectManifest_UnregisteredCwd_IsErrorWithHostGuidanceAndNoRetry).

        var properties = tools["read_project_manifest"]!["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("projectId");
    }

    [Fact]
    public async Task ReadProjectManifest_MissingProject_MapsToToolError()
    {
        Stub.Route("GET", $"/api/projects/{Canned.ProjectId}", 404, Canned.NotFoundJson);

        var response = await Driver.CallToolAsync("read_project_manifest", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("was not found");
    }
}
