using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class OnboardingToolContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        stub.RouteGet("/api/health", Canned.HealthJson);
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
    }

    [Fact]
    public async Task ToolsList_OnboardDescription_AllowsSkipWhenProjectIdIsExplicit()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_onboard"]!["description"]!.GetValue<string>();

        description.ShouldNotContain("Call this first");
        description.ShouldContain("may skip it");
        description.ShouldContain("OVERMIND_PROJECT_ID");
        description.ShouldContain("worktree");
    }

    [Fact]
    public async Task ToolsList_OnboardDescription_MentionsMaterializationAndValidationBlocks()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_onboard"]!["description"]!.GetValue<string>();

        description.ShouldContain("materialization block (strategy, targetDir, trigger, lastRun");
        description.ShouldContain("skipped-file count");
        description.ShouldContain("validation block (humanGate)");
    }

    [Fact]
    public async Task Onboard_PostsCwdAndReturnsFormattedSummary()
    {
        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var request = FindRequest("POST", "/api/onboard");
        request.Path.ShouldBe("/api/onboard");
        var body = JsonNode.Parse(request.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["projectId"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("alpha");
        text.ShouldContain(Canned.ProjectId.ToString());
        text.ShouldContain("Created project 'alpha'");
        text.ShouldContain("inProgress: 1");
        text.ShouldContain("draft: 1");
        text.ShouldContain("Current feature: demo-feature");
        text.ShouldContain(Canned.OnboardQuickstartText);
    }

    [Fact]
    public async Task Onboard_ProjectIdEnvOverride_SendsProjectIdInsteadOfCwd()
    {
        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_PROJECT_ID"] = Canned.ProjectId.ToString()
        });
        await driver.InitializeAsync();

        var response = await driver.CallToolAsync("overmind_onboard", new JsonObject());

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(request.Body)!;
        body["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
        body["cwd"].ShouldBeNull();

        Canned.ToolResultText(response).ShouldContain("alpha");
    }

    [Fact]
    public async Task Onboard_HostDown_ReturnsToolErrorAndProcessSurvives()
    {
        Stub.Stop();

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
        text.ShouldContain("unreachable");

        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_RendersMaterializationAndValidationBlocks()
    {
        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Materialization: strategy=on, targetDir=/repo/BACKLOG, lastRun=2026-09-23T10:15:00Z");
        text.ShouldContain("Validation: humanGate=off");
    }

    [Fact]
    public async Task Onboard_OffStrategy_ShowsTriggerExplanationAndNeverLastRun()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardOffStrategyJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Materialization: strategy=off, targetDir=/repo/BACKLOG, lastRun=never");
        text.ShouldContain("Materialization is off for this project — the materializer will not run.");
        text.ShouldContain("Enable it via the planning.materializationDefault setting or the per-project override.");
        text.ShouldContain("Validation: humanGate=on");
    }

    [Fact]
    public async Task Onboard_FailedMaterialization_ShowsErrorAndFailedAtOnLastRunLine()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardFailedMaterializationJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Materialization: strategy=on, targetDir=/repo/BACKLOG, lastRun=2026-09-23T10:15:00Z " +
            "(failed 2026-09-23T18:00:00Z: Refusing to overwrite locally modified tracked files:");
        text.ShouldContain("- /repo/BACKLOG/006-demo/tasks/01-first-task.md)");
    }

    [Fact]
    public async Task Onboard_SkippedMaterialization_ShowsCountAndHeadWithReasons()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardSkippedMaterializationJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Materialization: strategy=on, targetDir=/repo/BACKLOG, lastRun=2026-09-24T09:00:00Z");
        text.ShouldContain("Skipped on the last pass: 2 file(s)");
        text.ShouldContain("- BACKLOG/foreign/feature.md: file exists on disk but is not tracked by Overmind");
        text.ShouldContain("- BACKLOG/demo-feature/tasks/01-build.md: tracked file has local edits that are not on the board");
        text.ShouldNotContain("failed");
    }

    [Fact]
    public async Task Onboard_CyrillicPayload_ReachesClientAsLiteralText()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardCyrillicJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("кириллический проект");
        text.ShouldContain("<goal>");

        var frame = Driver.StdoutLines.FirstOrDefault(l =>
            l.Contains("кириллический") || l.Contains("\\u043A"));
        frame.ShouldNotBeNull("onboard response frame missing from stdout");
        frame!.ShouldContain("кириллический проект");
        frame.ShouldContain("Быстрый старт");
        frame.ShouldContain("<goal>");
        frame.ShouldNotContain("\\u04");
        frame.ShouldNotContain("\\u003C");
    }
}
