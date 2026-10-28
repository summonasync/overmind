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

        description.ShouldContain("materialization block (strategy, targetDir, trigger, lastSuccess vs lastAttempt");
        description.ShouldContain("skipped-file count");
        description.ShouldContain("a validation block (humanGate plus the state-truthful gate sentence");
        description.ShouldContain(
            "off: agent validate requires a live approval proof, overmind_notify(kind=approval) with a human reply " +
            "newer than the artifact's last update, 409 ValidationProofRequired without it");
        description.ShouldContain("on: agents STOP until a human validates the artifact in the UI");
    }

    [Fact]
    public async Task ToolsList_OnboardDescription_DocumentsBriefMode()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_onboard"]!["description"]!.GetValue<string>();

        description.ShouldContain(
            "brief=true returns only the project id, board counts per status, humanGate and the adapter version");
        description.ShouldContain("already onboarded and working a lane");
    }

    [Fact]
    public async Task Onboard_Brief_ReturnsOnlyTheFourBlocks()
    {
        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject { ["brief"] = true });

        var text = Canned.ToolResultText(response);
        text.ShouldContain("alpha");
        text.ShouldContain(Canned.ProjectId.ToString());
        text.ShouldContain("Board: inProgress: 1, draft: 1");
        text.ShouldContain("Validation: humanGate=off");

        text.ShouldNotContain("Created project");
        text.ShouldNotContain("Current feature:");
        text.ShouldNotContain("Materialization:");
        text.ShouldNotContain("Semantic search:");
        text.ShouldNotContain("Agent identity:");
        text.ShouldNotContain("Adapter:");
        text.ShouldNotContain(Canned.OnboardGateGuidanceOff);
        text.ShouldNotContain(Canned.OnboardQuickstartText);
    }

    [Fact]
    public async Task Onboard_Brief_WithoutFeatures_ShowsEmptyBoardLine()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardNoCountsJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject { ["brief"] = true });

        Canned.ToolResultText(response).ShouldContain("Board: no features yet.");
    }

    [Fact]
    public async Task Onboard_Brief_ShowsAdapterVersionAndWarningWhenPresent()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardAdapterMismatchJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject { ["brief"] = true });

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Adapter: 1.0.0+stalebuild — tools may be missing from your toolset; restart your MCP session");
    }

    [Fact]
    public async Task Onboard_Brief_PredatesAdapterBlock_ShowsUnknownVersionWithWarning()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardAdapterPredatesJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject { ["brief"] = true });

        Canned.ToolResultText(response).ShouldContain(
            "Adapter: unknown — adapter predates version reporting; restart recommended");
    }

    [Fact]
    public async Task Onboard_BriefFalse_MatchesDefaultPayloadByteForByte()
    {
        var explicitDefault = Canned.ToolResultText(
            await Driver.CallToolAsync("overmind_onboard", new JsonObject { ["brief"] = false }));
        var omitted = Canned.ToolResultText(await Driver.CallToolAsync("overmind_onboard", new JsonObject()));

        omitted.ShouldBe(explicitDefault);
        omitted.ShouldContain(Canned.OnboardQuickstartText);
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
        text.ShouldContain("Materialization: strategy=on, targetDir=/repo/BACKLOG, " +
            "lastSuccess=2026-09-23T10:15:00Z, lastAttempt=2026-09-23T10:15:00Z");
        text.ShouldContain($"Validation: humanGate=off — {Canned.OnboardGateGuidanceOff}");
    }

    [Fact]
    public async Task ToolsList_OnboardDescription_MentionsSemanticSearchBlock()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_onboard"]!["description"]!.GetValue<string>();

        description.ShouldContain("semantic search block (availability, with the verbatim probe reason when down)");
    }

    [Fact]
    public async Task ToolsList_OnboardDescription_MentionsAdapterBlock()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_onboard"]!["description"]!.GetValue<string>();

        description.ShouldContain(
            "adapter block (adapterVersion vs hostVersion, with a restart warning when they diverge or the adapter predates version reporting)");
    }

    [Fact]
    public async Task ToolsList_OnboardDescription_MentionsAgentIdentityBlock()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["overmind_onboard"]!["description"]!.GetValue<string>();

        description.ShouldContain(
            "agentIdentity block (effective alias/vendor resolved env > placeholder with its source, the project's naming rules for choosing your own stable alias, and the session_identity_set next step when the alias is still the shared placeholder)");
    }

    [Fact]
    public async Task Onboard_HealthySemanticBlock_ShowsAvailableAfterValidation()
    {
        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        var validationAt = text.IndexOf("Validation: humanGate=off", StringComparison.Ordinal);
        var semanticAt = text.IndexOf("Semantic search: available", StringComparison.Ordinal);
        validationAt.ShouldBeGreaterThanOrEqualTo(0);
        semanticAt.ShouldBeGreaterThan(validationAt);
    }

    [Fact]
    public async Task Onboard_UnavailableSemanticBlock_ShowsVerbatimProbeReason()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardSemanticUnavailableJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Semantic search: unavailable (model 'all-minilm-l6-v2' not found, HTTP 404)");
    }

    [Fact]
    public async Task Onboard_OffStrategy_ShowsTriggerExplanationAndNeverSplit()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardOffStrategyJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Materialization: strategy=off, targetDir=/repo/BACKLOG, lastSuccess=never, lastAttempt=never");
        text.ShouldContain("Materialization is off for this project — the materializer will not run.");
        text.ShouldContain("Enable it via the planning.materializationDefault setting or the per-project override.");
        text.ShouldContain("Validation: humanGate=on — agents STOP until a human validates the artifact in the UI.");
    }

    [Fact]
    public async Task Onboard_PayloadWithoutGuidance_RendersTheBareGateLine()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardSemanticUnavailableJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Validation: humanGate=off");
        text.ShouldNotContain("live approval proof");
    }

    [Fact]
    public async Task Onboard_FailedMaterialization_ShowsSuccessAttemptSplitWithFailureReason()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardFailedMaterializationJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Materialization: strategy=on, targetDir=/repo/BACKLOG, " +
            "lastSuccess=2026-09-23T10:15:00Z, lastAttempt=2026-09-23T18:00:00Z " +
            "(failed 2026-09-23T18:00:00Z: Refusing to overwrite locally modified tracked files:");
        text.ShouldContain("- /repo/BACKLOG/006-demo/tasks/01-first-task.md)");
    }

    [Fact]
    public async Task Onboard_SkippedMaterialization_ShowsCountAndHeadWithReasons()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardSkippedMaterializationJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Materialization: strategy=on, targetDir=/repo/BACKLOG, " +
            "lastSuccess=2026-09-24T09:00:00Z, lastAttempt=2026-09-24T09:00:00Z");
        text.ShouldContain("Skipped at the last successful pass (byte-identical no-ops): 2 file(s)");
        text.ShouldContain("- BACKLOG/foreign/feature.md: content already matches the board");
        text.ShouldContain("- BACKLOG/demo-feature/tasks/01-build.md: content already matches the board");
        text.ShouldNotContain("failed");
    }

    [Fact]
    public async Task Onboard_MismatchedAdapterBlock_ShowsVersionsAndRestartWarning()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardAdapterMismatchJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Adapter: 1.0.0+stalebuild vs host 1.0.0+freshbuild: tools may be missing from your toolset; restart your MCP session");
    }

    [Fact]
    public async Task Onboard_PredatesAdapterBlock_ShowsUnknownAdapterVersionAndPredatesWarning()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardAdapterPredatesJson);

        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Adapter: unknown vs host 1.0.0+freshbuild: adapter predates version reporting; restart recommended");
    }

    [Fact]
    public async Task Onboard_MissingAdapterBlock_RendersNothing()
    {
        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldNotContain("Adapter:");
    }

    [Fact]
    public async Task Onboard_RendersAgentIdentityBlockWithNamingRules()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardAgentIdentityJson);

        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_ALIAS"] = string.Empty,
            ["OVERMIND_VENDOR"] = string.Empty
        });
        await driver.InitializeAsync();

        var response = await driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Agent identity: alias 'agent', vendor 'unknown' (source: placeholder) — pass this pair to " +
            "planning_claim_feature when claiming a feature; the project's naming rules:");
        text.ShouldContain("Aliases on this project take the crew animal list and stay lowercase.");
        text.ShouldContain("Keep the alias stable across sessions — it binds the feature claim.");
        text.ShouldContain(
            "Next step: 'agent' is the shared placeholder — invent your own alias per the naming rules above " +
            "(or restore your stable one) and set it via session_identity_set BEFORE claiming");
        text.ShouldContain(
            "planning_list_lanes first and adopt/bind the lane's boundAlias via session_identity_set before the claim");
    }

    [Fact]
    public async Task Onboard_EnvIdentity_RendersSourceAndSkipsNextStep()
    {
        Stub.Route("POST", "/api/onboard", 200, Canned.OnboardAgentIdentityJson);

        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_ALIAS"] = "crew-otter",
            ["OVERMIND_VENDOR"] = "qwen"
        });
        await driver.InitializeAsync();

        var response = await driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldContain(
            "Agent identity: alias 'crew-otter', vendor 'qwen' (source: env) — pass this pair to " +
            "planning_claim_feature when claiming a feature; the project's naming rules:");
        text.ShouldNotContain("Next step:");
    }

    [Fact]
    public async Task Onboard_MissingAgentIdentityBlock_RendersNothing()
    {
        var response = await Driver.CallToolAsync("overmind_onboard", new JsonObject());

        var text = Canned.ToolResultText(response);
        text.ShouldNotContain("Agent identity:");
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
