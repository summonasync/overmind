using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class PlanningToolContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task ToolsList_ContainsAllPlanningTools()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        string[] expected =
        [
            "planning_claim_feature",
            "planning_list_features",
            "planning_read_feature",
            "planning_unclaim_feature",
            "planning_list_artifacts",
            "planning_read_artifact",
            "planning_read_artifact_diff",
            "planning_current_task",
            "planning_create_feature",
            "planning_upsert_artifact",
            "planning_upsert_tasks",
            "planning_set_status",
            "planning_append_log",
            "planning_edit_log",
            "planning_patch_artifact",
            "planning_delete_artifact",
            "planning_validate",
            "planning_set_feature_status",
            "planning_task_queue",
            "planning_update_feature",
            "planning_patch_feature",
            "planning_move_feature",
            "planning_set_feature_priority",
            "planning_feature_code",
            "planning_pin_feature_code",
            "planning_list_feature_dependencies",
            "planning_add_feature_dependency",
            "planning_remove_feature_dependency",
            "planning_materialize",
            "planning_materialize_status",
            "planning_import_feature",
            "planning_export_backlog"
        ];
        foreach (var name in expected)
        {
            tools.ShouldContainKey(name);
            tools[name]!["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        }

        tools["planning_claim_feature"]!["description"]!.GetValue<string>().ShouldContain("names the holder");
        tools["planning_claim_feature"]!["description"]!.GetValue<string>().ShouldContain("auto-adopt");
        tools["planning_claim_feature"]!["description"]!.GetValue<string>().ShouldContain("human door");
        tools["planning_unclaim_feature"]!["description"]!.GetValue<string>().ShouldContain("holder session only");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("claimState");
        tools["planning_read_feature"]!["description"]!.GetValue<string>().ShouldContain("claimedByAlias");
        tools["planning_list_features"]!["description"]!.GetValue<string>().ShouldContain("board");
        tools["planning_list_features"]!["description"]!.GetValue<string>()
            .ShouldContain("taskCounts: todo/inProgress/review/done/blocked, sums to taskTotal");
        tools["planning_current_task"]!["description"]!.GetValue<string>().ShouldContain("STOP");
        tools["planning_current_task"]!["description"]!.GetValue<string>().ShouldContain("unclosed blockers");
        tools["planning_set_status"]!["description"]!.GetValue<string>().ShouldContain("logNote");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("brief");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("goal, scope, anti-scope, constraints, acceptance-criteria, refs, obstacles, log");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("free-form markdown with <log> as the only machine-parsed section");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("lowest order first");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("first append lands on 0; materialized backlog files are numbered 1-based");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("omitted order appends after the current max");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("empty array clears");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("empty array clears");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("the UI owns feature deletion");
        tools["planning_create_feature"]!["description"]!.GetValue<string>().ShouldContain("priority");
        tools["planning_create_feature"]!["description"]!.GetValue<string>().ShouldContain("the UI owns feature deletion");
        tools["planning_create_feature"]!["description"]!.GetValue<string>().ShouldContain("then validate via planning_validate — send overmind_notify(kind=approval, featureSlug) and wait for the human's reply first");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("lowest order first");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("first append lands on 0; materialized backlog files are numbered 1-based");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("omitted order appends after the current max");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "Gaps in order numbering are legal and meaningful");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>()
            .ShouldContain("never renumber existing tasks to close a hole");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain(
            "Gaps in order numbering are legal and meaningful");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>()
            .ShouldContain("never renumber existing tasks to close a hole");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>()
            .ShouldContain("every response item echoes the resulting edge set as blockedBy");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>()
            .ShouldContain("[] when the item ends with no edges");
        tools["planning_read_artifact"]!["description"]!.GetValue<string>()
            .ShouldContain("Task payloads also carry blockedBy");
        tools["planning_read_artifact"]!["description"]!.GetValue<string>()
            .ShouldContain("non-task kinds omit the field");
        tools["planning_validate"]!["description"]!.GetValue<string>().ShouldContain("human validation");
        tools["planning_set_feature_status"]!["description"]!.GetValue<string>().ShouldContain("user's explicit request");
        tools["planning_set_feature_status"]!["description"]!.GetValue<string>().ShouldContain("human door");
        tools["planning_update_feature"]!["description"]!.GetValue<string>().ShouldContain("logNote is mandatory");
        tools["planning_update_feature"]!["description"]!.GetValue<string>().ShouldContain("tail of its priority band");
        tools["planning_update_feature"]!["description"]!.GetValue<string>().ShouldContain("planning_set_feature_status");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("priority asc");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("dragging");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("readiness 0-3");
        tools["planning_task_queue"]!["description"]!.GetValue<string>()
            .ShouldContain("taskCounts (todo/inProgress/review/done/blocked, sums to taskTotal)");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("briefExcerpt");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("createdAt");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("updatedAt");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("pendingMutations");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("plan validated");
        tools["planning_move_feature"]!["description"]!.GetValue<string>().ShouldContain("band-scoped");
        tools["planning_move_feature"]!["description"]!.GetValue<string>().ShouldContain("toPosition");
        tools["planning_move_feature"]!["description"]!.GetValue<string>().ShouldContain("planning_set_feature_priority");
        tools["planning_set_feature_priority"]!["description"]!.GetValue<string>().ShouldContain("explicit approval");
        tools["planning_set_feature_priority"]!["description"]!.GetValue<string>().ShouldContain("tail");
        tools["planning_set_feature_priority"]!["description"]!.GetValue<string>().ShouldContain("planning_move_feature");
        tools["planning_pin_feature_code"]!["description"]!.GetValue<string>().ShouldContain("naming convention");
        tools["planning_pin_feature_code"]!["description"]!.GetValue<string>().ShouldContain("planning_feature_code");
        tools["planning_pin_feature_code"]!["description"]!.GetValue<string>().ShouldContain("full replace");
        tools["planning_pin_feature_code"]!["description"]!.GetValue<string>().ShouldContain("no-op success");
        tools["planning_pin_feature_code"]!["description"]!.GetValue<string>().ShouldContain("one feature log line");
        tools["planning_list_feature_dependencies"]!["description"]!.GetValue<string>().ShouldContain("done or review");
        tools["planning_add_feature_dependency"]!["description"]!.GetValue<string>().ShouldContain("cycle");
        tools["planning_remove_feature_dependency"]!["description"]!.GetValue<string>().ShouldContain("no-op");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("acceptance");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "a tag-parseable name (the XML vocabulary — goal, scope, anti-scope, constraints, acceptance-criteria, refs, obstacles, log) " +
            "always routes to the XML section path byte-identically");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "a bare word like 'scope' on a spec creates/reaches the XML section, NOT the heading block: " +
            "heading addresses need the '## ' prefix");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "remove=true together with reason replaces the resolved heading block — heading line included — " +
            "with a single '<removed YYYY-MM-DD — reason>' line");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "a duplicate heading name is a 400 listing the matching headings");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "heading mode on a task is a 400 naming the XML vocabulary");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "removing an XML section is refused in v1 (RemoveNotAllowedForXmlSection");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "a heading patch on a validated artifact demotes it + needs logNote");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "a heading block carrying the <log> pair is refused the same way");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "acIndex is 1-based; numbering matches the materialized mirror; acText is the recommended selector");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "acText '&lt;key&gt;' matches a stored '<key>' and the reverse");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "an out-of-range acIndex or an unmatched acText is a 400 listing the visible items");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("untick=true");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("already unticked — no change");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("tickedIndex + tickedText");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("amendment door");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("'## <amendment> yyyy-MM-dd'");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("NO demotion");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("kind=result");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("newest <log> line");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("own actor marker");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("[agent] on this door, [ui] on the human door");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("single line");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("version-traced");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("Feature-level logs are out of scope");
        tools["planning_edit_log"]!["description"]!.GetValue<string>().ShouldContain("planning_append_log");
        tools["planning_delete_artifact"]!["description"]!.GetValue<string>().ShouldContain(
            "agents can delete draft artifacts only; validated/done deletion is the human's door in the UI");
        tools["planning_delete_artifact"]!["description"]!.GetValue<string>().ShouldContain("clean up dependencies first");
        tools["planning_delete_artifact"]!["description"]!.GetValue<string>().ShouldContain("soft delete");
        tools["planning_materialize"]!["description"]!.GetValue<string>().ShouldContain("board mutation");
        tools["planning_materialize_status"]!["description"]!.GetValue<string>().ShouldContain("stalePaths");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("all-or-nothing");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("one feature auto-transition evaluation");
        tools["planning_import_feature"]!["description"]!.GetValue<string>().ShouldContain("never pass through the model");
        tools["planning_export_backlog"]!["description"]!.GetValue<string>().ShouldContain("commit the planning alongside the code");
        tools["planning_export_backlog"]!["description"]!.GetValue<string>().ShouldContain("plain greppable files");
        tools["planning_export_backlog"]!["description"]!.GetValue<string>().ShouldContain("no body echo");

        tools.Keys.Count(name => name.StartsWith("planning_", StringComparison.Ordinal)).ShouldBe(39);
    }

    [Fact]
    public async Task PricedLadder_CarriesTierPhrases_NineRungs()
    {
        var response = await Driver.ListToolsAsync();

        var descriptions = response["result"]!["tools"]!.AsArray()
            .Select(t => (Name: t!["name"]!.GetValue<string>(), Text: t["description"]!.GetValue<string>()))
            .Where(pair => pair.Name.StartsWith("planning_", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Name, pair => pair.Text);

        descriptions["planning_append_log"].ShouldContain("cheapest write");
        descriptions["planning_pin_feature_code"].ShouldContain("full replace");
        descriptions["planning_edit_log"].ShouldContain("cheap write tier");
        descriptions["planning_patch_artifact"].ShouldContain("middle tier");
        descriptions["planning_upsert_artifact"].ShouldContain("most expensive write");
        descriptions["planning_read_artifact"].ShouldContain("cheapest read");
        descriptions["planning_read_artifact_diff"].ShouldContain("no full bodies, only the change hunks");
        descriptions["planning_read_feature"].ShouldContain("most expensive read");

        descriptions.Count(pair => pair.Value.Contains("most expensive", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Fact]
    public async Task PlanningListFeatures_ProxiesAgentBoardRoute()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features");
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/features");

        Canned.ToolResultText(response).ShouldContain("demo-feature");
        Canned.ToolResultText(response).ShouldContain("\"phase\":\"in progress\"");
        Canned.ToolResultText(response).ShouldContain("\"briefExcerpt\":\"Crash on save when the workspace file is read-only.\"");
        Canned.ToolResultText(response).ShouldContain("\"createdAt\":\"2026-09-08T09:00:00Z\"");
        Canned.ToolResultText(response).ShouldNotContain("isCurrent");
        Canned.ToolResultText(response).ShouldContain("\"priority\":\"p1\"");
        Canned.ToolResultText(response).ShouldContain("\"taskCounts\":{\"todo\":0,\"inProgress\":1,\"review\":0,\"done\":1,\"blocked\":0}");
    }

    [Fact]
    public async Task PlanningListFeatures_ForwardsFiltersThroughTheQuery()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["status"] = "inProgress",
            ["claimedByAlias"] = "Dan",
            ["lane"] = "frontend",
            ["priority"] = "p1",
            ["unclaimedOnly"] = true
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features");
        var query = ParseQuery(request.Query);
        query.ShouldBe(new Dictionary<string, string>
        {
            ["status"] = "inProgress",
            ["claimedByAlias"] = "Dan",
            ["lane"] = "frontend",
            ["priority"] = "p1",
            ["unclaimedOnly"] = "true"
        });
    }

    [Theory]
    [InlineData("status", "")]
    [InlineData("priority", "   ")]
    public async Task PlanningListFeatures_BlankFilterValues_AreDroppedFromTheQuery(string field, string value)
    {
        await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            [field] = value
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features");
        ParseQuery(request.Query).ShouldNotContainKey(field);
    }

    [Theory]
    [InlineData("planning_list_features", "{}", "GET",
        "/api/agent/projects/{projectId}/planning/features")]
    [InlineData("planning_task_queue", "{}", "GET",
        "/api/agent/projects/{projectId}/planning/task-queue")]
    [InlineData("planning_materialize_status", "{}", "GET",
        "/api/agent/projects/{projectId}/planning/materialization-status")]
    [InlineData("planning_read_feature", """{"slug":"demo-feature"}""", "GET",
        "/api/agent/projects/{projectId}/planning/features/demo-feature")]
    [InlineData("planning_list_artifacts", """{"slug":"demo-feature"}""", "GET",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/artifacts")]
    [InlineData("planning_current_task", """{"slug":"demo-feature"}""", "GET",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/current")]
    [InlineData("planning_create_feature", """{"slug":"new-feature","title":"Demo"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features")]
    [InlineData("planning_upsert_artifact", """{"featureSlug":"demo-feature","kind":"spec","body":"<goal>body</goal>"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/artifacts")]
    [InlineData("planning_upsert_tasks", """{"featureSlug":"demo-feature","tasks":[{"body":"# Task"}]}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/artifacts/tasks")]
    [InlineData("planning_set_feature_status", """{"slug":"demo-feature","status":"inProgress","logNote":"note"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/status")]
    [InlineData("planning_update_feature", """{"slug":"demo-feature","logNote":"note"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/update")]
    [InlineData("planning_move_feature", """{"slug":"demo-feature","move":"up"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/queue-move")]
    [InlineData("planning_claim_feature", """{"slug":"demo-feature","alias":"Dan","vendor":"claude"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/claim")]
    [InlineData("planning_unclaim_feature", """{"slug":"demo-feature"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/unclaim")]
    [InlineData("planning_set_feature_priority", """{"slug":"demo-feature","priority":"p1"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/priority")]
    [InlineData("planning_pin_feature_code", """{"slug":"demo-feature"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/pin-code")]
    [InlineData("planning_list_feature_dependencies", """{"slug":"demo-feature"}""", "GET",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/dependencies")]
    [InlineData("planning_add_feature_dependency", """{"slug":"demo-feature","blockedBySlug":"other-feature"}""", "POST",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/dependencies")]
    [InlineData("planning_remove_feature_dependency", """{"slug":"demo-feature","blockedBySlug":"other-feature"}""", "DELETE",
        "/api/agent/projects/{projectId}/planning/features/demo-feature/dependencies/other-feature")]
    [InlineData("planning_import_feature", """{"path":"/repo/BACKLOG/058-agent-side-import"}""", "POST",
        "/api/agent/projects/{projectId}/planning/import")]
    [InlineData("planning_export_backlog", """{"targetPath":"/tmp/demo-snapshot"}""", "POST",
        "/api/agent/projects/{projectId}/planning/export")]
    [InlineData("planning_materialize", """{"force":true}""", "POST",
        "/api/projects/{projectId}/planning/materialize")]
    public async Task PlanningTools_WithoutProjectId_ResolveThroughTheOnboardCwdThenProxy(
        string tool, string arguments, string method, string expectedPath)
    {
        var response = await Driver.CallToolAsync(tool, JsonNode.Parse(arguments)!.AsObject());

        response["result"]!["isError"].ShouldBeNull();

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var onboardBody = JsonNode.Parse(onboard.Body)!;
        onboardBody["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        onboardBody["create"]!.GetValue<bool>().ShouldBeFalse();

        Stub.Requests.ShouldContain(r =>
            r.Method == method && r.Path == expectedPath.Replace("{projectId}", Canned.ProjectId.ToString()));
    }

    [Fact]
    public async Task PlanningTools_ExplicitProjectId_SkipsTheOnboardRoundTrip()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features");
    }

    [Fact]
    public async Task PlanningTools_ProjectIdEnvFallback_SkipsTheOnboardRoundTrip()
    {
        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_PROJECT_ID"] = Canned.ProjectId.ToString()
        });
        await driver.InitializeAsync();

        var response = await driver.CallToolAsync("planning_task_queue", new JsonObject());

        response["result"]!["isError"].ShouldBeNull();
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/task-queue");
    }

    [Fact]
    public async Task PlanningTools_ExplicitProjectId_WinsOverTheEnvFallback()
    {
        using var driver = McpProcessDriver.Start(Stub.BaseUrl, new Dictionary<string, string>
        {
            ["OVERMIND_PROJECT_ID"] = Canned.SecondProjectId.ToString()
        });
        await driver.InitializeAsync();

        var response = await driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        response["result"]!["isError"].ShouldBeNull();
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path == "/api/onboard");
        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features");
        Stub.Requests.ShouldNotContain(r =>
            r.Path == $"/api/agent/projects/{Canned.SecondProjectId}/planning/features");
    }

    [Fact]
    public async Task PlanningTools_UnresolvableCwd_ReturnsTheGuidanceWithoutHittingPlanning()
    {
        Stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);

        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject());

        var text = ToolErrorText(response);
        text.ShouldContain("Could not resolve the project for the working directory");
        text.ShouldContain("No project is registered for the working directory");
        text.ShouldContain("Pass projectId explicitly");
        text.ShouldContain("overmind_onboard");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningReadFeature_ProxiesAgentFeatureRoute()
    {
        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features/");
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Demo spec");
        text.ShouldContain("Ship the demo.");
    }

    [Fact]
    public async Task PlanningReadArtifact_ProxiesAgentArtifactRoute()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}");
        request.Query.ShouldBeEmpty();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("# Wire the API");
        text.ShouldContain("\"brief\":\"Wire the endpoints\"");
        text.ShouldContain("\"review\":\"hard\"");
        text.ShouldNotContain("missingSections");
    }

    [Fact]
    public async Task PlanningReadArtifact_DropsReviewFromDocumentKinds()
    {
        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+",
            Canned.PlanningArtifactSpecReadJson);

        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString()
        });

        var artifact = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        artifact["kind"]!.GetValue<string>().ShouldBe("spec");
        artifact.ContainsKey("review").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadArtifact_CarriesBlockedByOnTasksOnly()
    {
        var taskRead = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync(
            "planning_read_artifact",
            new JsonObject { ["artifactId"] = Canned.PlanningTaskTwoId.ToString() })))!.AsObject();
        taskRead["blockedBy"]!.ToJsonString().ShouldBe("[1]");

        var sectioned = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync(
            "planning_read_artifact",
            new JsonObject
            {
                ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
                ["sections"] = "goal"
            })))!.AsObject();
        sectioned["blockedBy"]!.ToJsonString().ShouldBe("[1]");

        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+",
            Canned.PlanningArtifactSpecReadJson);

        var specRead = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync(
            "planning_read_artifact",
            new JsonObject { ["artifactId"] = Canned.PlanningSpecArtifactId.ToString() })))!.AsObject();
        specRead.ContainsKey("blockedBy").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadFeature_KeepsReviewOnTasks_DropsItFromDocuments()
    {
        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var feature = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        var artifacts = feature["artifacts"]!.AsArray();
        var byId = artifacts.ToDictionary(a => a!["id"]!.GetValue<string>(), a => a!.AsObject());

        byId[Canned.PlanningSpecArtifactId.ToString()]!.ContainsKey("review").ShouldBeFalse();
        byId[Canned.PlanningPlanArtifactId.ToString()]!.ContainsKey("review").ShouldBeFalse();
        byId[Canned.PlanningTaskOneId.ToString()]!["review"]!.GetValue<string>().ShouldBe("light");
        byId[Canned.PlanningTaskTwoId.ToString()]!["review"]!.GetValue<string>().ShouldBe("hard");
    }

    [Fact]
    public async Task PlanningReadFeature_TaskDependencies_PassThroughOnTasksOnly()
    {
        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var artifacts = JsonNode.Parse(Canned.ToolResultText(response))!["artifacts"]!.AsArray();
        var byId = artifacts.ToDictionary(a => a!["id"]!.GetValue<string>(), a => a!.AsObject());

        byId[Canned.PlanningTaskTwoId.ToString()]!["blocked"]!.GetValue<bool>().ShouldBeFalse();
        byId[Canned.PlanningTaskTwoId.ToString()]!["blockedBy"]!.ToJsonString().ShouldBe("[1]");
        byId[Canned.PlanningTaskOneId.ToString()]!.ContainsKey("blocked").ShouldBeFalse();
        byId[Canned.PlanningSpecArtifactId.ToString()]!.ContainsKey("blocked").ShouldBeFalse();
        byId[Canned.PlanningSpecArtifactId.ToString()]!.ContainsKey("blockedBy").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningListArtifacts_ProxiesAgentRosterRoute_Unchanged()
    {
        var response = await Driver.CallToolAsync("planning_list_artifacts", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts");
        request.Query.ShouldBeEmpty();

        var roster = JsonNode.Parse(Canned.ToolResultText(response))!;
        JsonNode.DeepEquals(roster, JsonNode.Parse(Canned.PlanningArtifactRosterJson)).ShouldBeTrue();

        var items = roster["artifacts"]!.AsArray();
        items.Count.ShouldBe(4);
        items.ShouldNotContain(i => i!.AsObject().ContainsKey("body"));
        items.ShouldNotContain(i => i!.AsObject().ContainsKey("log"));
        var spec = items.Single(i => i!["kind"]!.GetValue<string>() == "spec")!.AsObject();
        spec.ContainsKey("review").ShouldBeFalse();
        spec["pointers"]!.AsArray().Count.ShouldBe(1);
        spec["pointers"]![0]!["path"]!.GetValue<string>().ShouldBe("docs/demo-spec.md");
        spec["pointers"]![0]!["anchor"]!.GetValue<string>().ShouldBe("non-goals");
        items.Single(i => i!["kind"]!.GetValue<string>() == "plan")!["pointers"]!.ToJsonString().ShouldBe("[]");
        var task = items.Single(i => i!["artifactId"]!.GetValue<string>() == Canned.PlanningTaskTwoId.ToString())!.AsObject();
        task["review"]!.GetValue<string>().ShouldBe("hard");
        task["acTicked"]!.GetValue<int>().ShouldBe(0);
        task["acTotal"]!.GetValue<int>().ShouldBe(3);
        task["brief"]!.GetValue<string>().ShouldBe("Wire the endpoints");
        task["blocked"]!.GetValue<bool>().ShouldBeFalse();
        task["blockedBy"]!.ToJsonString().ShouldBe("[1]");
        task["pointers"]!.ToJsonString().ShouldBe("[]");
    }

    [Fact]
    public async Task PlanningListArtifacts_KindFilter_IsForwardedInTheRequestUrl()
    {
        var response = await Driver.CallToolAsync("planning_list_artifacts", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["kind"] = "task"
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts");
        ParseQuery(request.Query)["kind"].ShouldBe("task");
    }

    [Fact]
    public async Task PlanningListArtifacts_UnknownFeature_SurfacesApiErrorAsToolError()
    {
        Stub.Route(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts",
            404,
            Canned.PlanningFeatureNotFoundJson);

        var response = await Driver.CallToolAsync("planning_list_artifacts", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        ToolErrorText(response).ShouldContain("Feature 'demo-feature' was not found.");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningListArtifacts_Description_PinsTheRosterPitch()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_list_artifacts"]!["description"]!.GetValue<string>().ShouldBe(
            "Metadata-only roster of one feature's artifacts — the cheapest way to see what a feature contains; planning_read_artifact for one body, planning_read_feature for the whole feature. " +
            "Every row carries additive pointers: [{path, anchor}] parsed from pointer lines in the body — an artifact can point at a repo file instead of copying it; the server never reads the targets (empty array when none).");
    }

    [Fact]
    public async Task PlanningUpsertArtifact_Parameters_PinTheBodyPointerAndTheOrderHandle()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_upsert_artifact")!;
        var properties = tool["inputSchema"]!["properties"]!.AsObject();

        var body = properties["body"]!["description"]!.GetValue<string>();
        body.ShouldContain("Expected XML sections per kind are listed in this tool's description");
        body.ShouldContain("plain markdown is accepted");
        body.ShouldNotContain("with its XML sections");

        var order = properties["order"]!["description"]!.GetValue<string>();
        order.ShouldContain("Sort key and stable handle");
        order.ShouldContain("gaps are legal — do not renumber to close them");
    }

    [Fact]
    public async Task PlanningUpsertTools_ExposeDryRun_AsOptionalBoolean()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        foreach (var name in new[] { "planning_upsert_artifact", "planning_upsert_tasks" })
        {
            var properties = tools[name]!["inputSchema"]!["properties"]!.AsObject();
            properties.ShouldContainKey("dryRun");
            properties["dryRun"]!["description"]!.GetValue<string>().ShouldContain("persist nothing");

            var required = tools[name]!["inputSchema"]!["required"]?.AsArray();
            if (required is not null)
            {
                required.ShouldNotContain(node => node!.GetValue<string>() == "dryRun");
            }

            tools[name]!["description"]!.GetValue<string>().ShouldContain("dryRun=true");
        }
    }

    [Fact]
    public async Task PlanningReadArtifact_WithSections_PassesTheQueryAndMirrorsTheProjection()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["sections"] = "goal,obstacles"
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}");
        ParseQuery(request.Query)["sections"].ShouldBe("goal,obstacles");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Expose the endpoints.");
        text.ShouldContain("\"missingSections\":[\"obstacles\"]");
    }

    [Fact]
    public async Task PlanningReadArtifact_UnknownArtifact_SurfacesApiErrorAsToolError()
    {
        Stub.Route(
            "GET",
            $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}",
            404,
            Canned.PlanningArtifactNotFoundJson);

        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Artifact '99999999-9999-9999-9999-999999999999' was not found");
        text.ShouldContain("<slug>/T<order>");
        text.ShouldContain("planning_list_artifacts");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadArtifact_TaskAlias_ResolvesThroughTheRosterAndReadsTheArtifact()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        Stub.Requests.Single(r =>
            r.Method == "GET" &&
            r.Path == $"/api/agent/projects/{Canned.ProjectId}/planning/features/demo-feature/artifacts");

        var read = Stub.Requests.Single(r =>
            r.Method == "GET" && r.Path.StartsWith("/api/agent/planning/artifacts/", StringComparison.Ordinal));
        read.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}");

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldContain("# Wire the API");
    }

    [Theory]
    [InlineData("demo-feature/S", "66666666-6666-6666-6666-666666666666")]
    [InlineData("demo-feature/P", "77777777-7777-7777-7777-777777777777")]
    public async Task PlanningReadArtifact_DocumentAlias_MatchesTheSingleKindIgnoringOrder(string alias, string expectedId)
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = alias
        });

        FindRequest("GET", $"/api/agent/planning/artifacts/{expectedId}");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningReadArtifact_ResultAlias_ResolvesAgainstARosterWithAResult()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts",
            """{"artifacts":[{"artifactId":"55555555-5555-5555-5555-555555555555","kind":"result","status":"done","version":1,"order":0,"updatedAt":"2026-09-10T07:30:00Z"}]}""");

        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/R"
        });

        FindRequest("GET", "/api/agent/planning/artifacts/55555555-5555-5555-5555-555555555555");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningReadArtifact_DuplicateDocuments_AliasReportsEveryCandidateInsteadOfPickingOne()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts",
            """
            {"artifacts":[
              {"artifactId":"66666666-6666-6666-6666-666666666666","kind":"spec","status":"validated","version":2,"order":0,"updatedAt":"2026-09-09T10:00:00Z"},
              {"artifactId":"44444444-4444-4444-4444-444444444441","kind":"spec","status":"draft","version":1,"order":1,"updatedAt":"2026-09-10T10:00:00Z"},
              {"artifactId":"77777777-7777-7777-7777-777777777777","kind":"plan","status":"draft","version":1,"order":0,"updatedAt":"2026-09-08T11:00:00Z"}
            ]}
            """);

        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/S"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("demo-feature/S");
        text.ShouldContain("demo-feature");
        text.ShouldContain("2 spec artifacts");
        text.ShouldContain("spec@0");
        text.ShouldContain("spec@1");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "GET" && r.Path.StartsWith("/api/agent/planning/artifacts/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningAppendLog_TaskAlias_PostsToTheResolvedArtifact()
    {
        var response = await Driver.CallToolAsync("planning_append_log", new JsonObject
        {
            ["note"] = "aliased progress note",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T1"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/log", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskOneId}/log");
        JsonNode.Parse(request.Body)!["note"]!.GetValue<string>().ShouldBe("aliased progress note");

        Canned.ToolResultText(response).ShouldContain("log line recorded on the board");
    }

    [Fact]
    public async Task PlanningEditLog_PutsTheReplacementText()
    {
        var response = await Driver.CallToolAsync("planning_edit_log", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["text"] = "halfway through the wiring, corrected"
        });

        var request = Stub.Requests.Single(r => r.Method == "PUT" && r.Path.EndsWith("/log", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/log");
        var posted = JsonNode.Parse(request.Body)!;
        posted["text"]!.GetValue<string>().ShouldBe("halfway through the wiring, corrected");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine"]);
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        echo["version"]!.GetValue<int>().ShouldBe(3);
        echo["status"]!.GetValue<string>().ShouldBe("inProgress");
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("log line recorded on the board");
    }

    [Fact]
    public async Task PlanningEditLog_TaskAlias_PutsToTheResolvedArtifact()
    {
        var response = await Driver.CallToolAsync("planning_edit_log", new JsonObject
        {
            ["text"] = "aliased correction",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T1"
        });

        var request = Stub.Requests.Single(r => r.Method == "PUT" && r.Path.EndsWith("/log", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskOneId}/log");
        JsonNode.Parse(request.Body)!["text"]!.GetValue<string>().ShouldBe("aliased correction");

        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningEditLog_WrongActorRefusal_SurfacesTheHostDetail()
    {
        Stub.Route(
            "PUT",
            $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/log",
            409,
            """{"title":"LogEditWrongActor","detail":"The newest log line carries the [ui] marker, and the log holds no [agent] line to edit — only lines carrying your own door marker are yours. Append a new line via planning_append_log instead.","status":409}""");

        var response = await Driver.CallToolAsync("planning_edit_log", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["text"] = "correction across the door"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("[ui]");
        text.ShouldContain("own door marker");
        text.ShouldContain("planning_append_log");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningSetStatus_Alias_MovesTheResolvedArtifact()
    {
        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "aliased status move",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/status");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningPatchArtifact_TaskAlias_PatchesTheResolvedArtifact()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["section"] = "goal",
            ["content"] = "<goal>\nAliased edit.\n</goal>",
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/patch");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningDeleteArtifact_DraftTask_DeletesAndEchoesTheFeatureAuditLine()
    {
        var response = await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["logNote"] = "duplicate from upsert without order"
        });

        var request = FindRequest("DELETE", $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}");
        var body = JsonNode.Parse(request.Body)!;
        body["logNote"]!.GetValue<string>().ShouldBe("duplicate from upsert without order");
        body["source"]!.GetValue<string>().ShouldBe("agent");

        response["result"]!["isError"].ShouldBeNull();
        var echo = JsonNode.Parse(Canned.ToolResultText(response))!;
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        echo["kind"]!.GetValue<string>().ShouldBe("task");
        echo["featureLogLine"]!.GetValue<string>().ShouldBe(
            "artifact task 02 deleted by agent, duplicate from upsert without order");
    }

    [Fact]
    public async Task PlanningDeleteArtifact_TaskAlias_ResolvesThroughTheRoster()
    {
        var response = await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2",
            ["logNote"] = "aliased cleanup"
        });

        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features/demo-feature/artifacts");
        FindRequest("DELETE", $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningDeleteArtifact_Source_TrimmedAndFallsBackToDoorSource()
    {
        await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["logNote"] = "marked run",
            ["source"] = " ops-bot "
        });

        var marked = JsonNode.Parse(FindRequest("DELETE", $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}").Body)!;
        marked["source"]!.GetValue<string>().ShouldBe("ops-bot");

        await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskOneId.ToString(),
            ["logNote"] = "blank source",
            ["source"] = "   "
        });

        var fallback = JsonNode.Parse(FindRequest("DELETE", $"/api/agent/planning/artifacts/{Canned.PlanningTaskOneId}").Body)!;
        fallback["source"]!.GetValue<string>().ShouldBe("agent");
    }

    [Fact]
    public async Task PlanningDeleteArtifact_ValidatedDocument_RefusedByTheGuard()
    {
        Stub.Route(
            "DELETE",
            $"/api/agent/planning/artifacts/{Canned.PlanningSpecArtifactId}",
            409,
            Canned.PlanningArtifactDeletionRefusedJson);

        var response = await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("deletes draft artifacts only");
        text.ShouldContain("human door in the Overmind UI");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningDeleteArtifact_TaskWithDependents_RefusedNamingTheCleanup()
    {
        Stub.Route(
            "DELETE",
            $"/api/agent/planning/artifacts/{Canned.PlanningTaskOneId}",
            409,
            Canned.PlanningArtifactDeletionBlockedJson);

        var response = await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskOneId.ToString()
        });

        ToolErrorText(response).ShouldContain("Clean up dependencies first");
    }

    [Fact]
    public async Task PlanningDeleteArtifact_UnknownArtifact_404CarriesTheAliasHint()
    {
        Stub.Route(
            "DELETE",
            $"/api/agent/planning/artifacts/{Canned.MissingEntryId}",
            404,
            Canned.PlanningArtifactNotFoundJson);

        var response = await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.MissingEntryId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("artifact aliases");
        text.ShouldContain("planning_list_artifacts");
    }

    [Fact]
    public async Task PlanningDeleteArtifact_AliasWithArtifactId_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_delete_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        ToolErrorText(response).ShouldContain("not both");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningReadArtifact_UnknownTaskOrder_ListsThePresentKindOrderPairs()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T9"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("No artifact matches alias 'demo-feature/T9'");
        text.ShouldContain("spec/0, plan/0, task/1, task/2");
        Stub.Requests.ShouldNotContain(r =>
            r.Path.StartsWith("/api/agent/planning/artifacts/", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadArtifact_ResultAliasWithoutAResult_ListsThePresentKindOrderPairs()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/R"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("No artifact matches alias 'demo-feature/R'");
        text.ShouldContain("spec/0, plan/0, task/1, task/2");
        Stub.Requests.ShouldNotContain(r =>
            r.Path.StartsWith("/api/agent/planning/artifacts/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("T3")]
    [InlineData("demo-feature/")]
    [InlineData("demo-feature/X5")]
    [InlineData("demo-feature/T3/extra")]
    [InlineData("demo-feature/T")]
    [InlineData("demo-feature/T3x")]
    public async Task PlanningReadArtifact_MalformedAlias_RejectedWithoutHttpCall(string alias)
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = alias
        });

        var text = ToolErrorText(response);
        text.ShouldContain($"Invalid artifact alias '{alias}'");
        text.ShouldContain("<slug>/T<order>");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningReadArtifact_AliasWithArtifactId_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        ToolErrorText(response).ShouldContain("not both");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningReadArtifact_AliasWithoutProjectId_RejectedNamingTheRequirement()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact", new JsonObject
        {
            ["alias"] = "demo-feature/T2"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("alias requires projectId");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningSetStatus_NeitherArtifactIdNorAlias_RejectedClientSide()
    {
        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "orphan probe"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Provide artifactId, or alias together with projectId");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpsertArtifactAndCurrentTaskDescriptions_CarryTheNoPlanReviewRule()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        foreach (var name in new[] { "planning_upsert_artifact", "planning_current_task" })
        {
            var description = tools[name]!["description"]!.GetValue<string>();
            description.ShouldContain("Without a validated plan the feature never auto-promotes to review");
            description.ShouldContain("flip it manually via planning_set_feature_status");
        }
    }

    [Fact]
    public async Task PlanningReadArtifact_Description_PinsTheReadCostTiers()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_read_artifact"]!["description"]!.GetValue<string>().ShouldBe(
            "Reads one artifact by id — without sections, one artifact, full body; with sections, the cheapest read: only the named sections. " +
            "Sections reads are deterministic: located on the canonicalized form (the same one writes produce), found sections return in body order regardless of request order, and a name the body carries never lands in missingSections; the full read stays byte-faithful to storage. " +
            "missingSections names requested-but-absent sections; a read never fails on a missing one. " +
            "The review field appears on task payloads only. " +
            "Task payloads also carry blockedBy — the sibling task orders this task waits on, [] when it has no edges; non-task kinds omit the field. " +
            "Bodies may carry pointer markers — case-sensitive 'pointer:' as the first non-whitespace token naming either a repo-relative path with an optional #anchor or a grimoire crew-memory id ('grimoire://memory/<id>'); the server never reads the targets. " +
            "Parsed pointers ride the artifact projections (planning_current_task payload, planning_list_artifacts roster rows), not this read.");
    }

    [Fact]
    public async Task PlanningReadFeature_Description_PinsTheMostExpensiveReadTier()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_read_feature"]!["description"]!.GetValue<string>().ShouldContain("claimedByAlias");
        tools["planning_read_feature"]!["description"]!.GetValue<string>().ShouldContain("most expensive read");
    }

    [Fact]
    public async Task PlanningReadFeature_Sections_ParameterPinsTheOptionalCheapRead()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        var tool = tools["planning_read_feature"]!;
        var description = tool["description"]!.GetValue<string>();
        description.ShouldContain("missingSections");
        description.ShouldContain("byte-faithful to storage");

        var properties = tool["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("sections");
        properties["sections"]!["description"]!.GetValue<string>().ShouldContain("returned in body order");

        var required = tool["inputSchema"]!["required"]?.AsArray();
        if (required is not null)
        {
            required.ShouldNotContain(node => node!.GetValue<string>() == "sections");
        }
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_Description_PinsTheLadderAdjacencyAndTrailingNewline()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_read_artifact_diff"]!["description"]!.GetValue<string>().ShouldBe(
            "Diffs two versions of one artifact as a unified diff — the read between planning_read_artifact and " +
            "nothing: no full bodies, only the change hunks. fromVersion/toVersion optional: omitting both diffs " +
            "the two most recent versions. Adjacent versions may carry log-only bumps, so an empty diff between " +
            "neighbors is a valid answer, not an error. Trailing-newline semantics: a trailing newline is ignored, " +
            "so 'x' vs 'x\\n' diffs as empty (unlike git there is no \\ No newline marker). An unknown from/to " +
            "version fails with ArtifactVersionNotFound 404; from == to or from > to fails with InvalidVersionRange 400.");
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_Parameters_PinTheOptionalVersionPairAndAliasTail()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_read_artifact_diff")!;
        var properties = tool["inputSchema"]!["properties"]!.AsObject();

        properties.ShouldContainKey("artifactId");
        properties.ShouldContainKey("projectId");
        properties.ShouldContainKey("alias");
        properties["fromVersion"]!["description"]!.GetValue<string>().ShouldContain("defaults to toVersion - 1");
        properties["toVersion"]!["description"]!.GetValue<string>().ShouldContain("defaults to the artifact's current version");
        properties["alias"]!["description"]!.GetValue<string>().ShouldContain("'<slug>/T<order>'");
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_DefaultPair_HitsTheAgentDiffRouteWithoutQuery()
    {
        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+/diff",
            Canned.PlanningArtifactDiffJson);

        var response = await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/diff");
        request.Query.ShouldBeEmpty();

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldContain("@@ -1 +1 @@");
        Canned.ToolResultText(response).ShouldContain("+to");
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_ExplicitVersions_AreForwardedAsFromAndTo()
    {
        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+/diff",
            Canned.PlanningArtifactDiffJson);

        await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["fromVersion"] = 1,
            ["toVersion"] = 3
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        ParseQuery(request.Query)["from"].ShouldBe("1");
        ParseQuery(request.Query)["to"].ShouldBe("3");
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_FromVersionOnly_ForwardsFromWithoutTo()
    {
        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+/diff",
            Canned.PlanningArtifactDiffJson);

        await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["fromVersion"] = 2
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        ParseQuery(request.Query)["from"].ShouldBe("2");
        ParseQuery(request.Query).ShouldNotContainKey("to");
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_ToVersionOnly_ForwardsToWithoutFrom()
    {
        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+/diff",
            Canned.PlanningArtifactDiffJson);

        await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["toVersion"] = 4
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        ParseQuery(request.Query).ShouldNotContainKey("from");
        ParseQuery(request.Query)["to"].ShouldBe("4");
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_TaskAlias_ResolvesThroughTheRosterThenDiffs()
    {
        Stub.RouteGet(
            "/api/agent/planning/artifacts/[^/]+/diff",
            Canned.PlanningArtifactDiffJson);

        var response = await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        var request = FindRequest("GET", "/api/agent/planning/artifacts/");
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/diff");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_NeitherArtifactIdNorAlias_RejectedClientSide()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject());

        ToolErrorText(response).ShouldContain("Provide artifactId, or alias together with projectId");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_AliasWithArtifactId_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["projectId"] = Canned.ProjectId.ToString(),
            ["alias"] = "demo-feature/T2"
        });

        ToolErrorText(response).ShouldContain("not both");
        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_UnknownVersion_SurfacesThe404Detail()
    {
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+/diff",
            404,
            """{"title":"Not Found","detail":"Artifact '99999999-9999-9999-9999-999999999999' has no version 5: the live body is version 2; stored snapshots cover 1..1.","status":404}""");

        var response = await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["fromVersion"] = 5
        });

        ToolErrorText(response).ShouldContain("has no version 5");
        ToolErrorText(response).ShouldContain("stored snapshots cover 1..1");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadArtifactDiff_InvalidRange_SurfacesThe400Detail()
    {
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+/diff",
            400,
            """{"title":"InvalidVersionRange","detail":"Invalid version range: from and to are both 2; there is nothing to diff.","status":400}""");

        var response = await Driver.CallToolAsync("planning_read_artifact_diff", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["fromVersion"] = 2,
            ["toVersion"] = 2
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Invalid version range");
        text.ShouldContain("nothing to diff");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadFeature_IncludeArtifactsFalse_IsForwardedInTheRequestUrl()
    {
        await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["includeArtifacts"] = false
        });

        var request = FindRequest(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");
        ParseQuery(request.Query)["includeArtifacts"].ShouldBe("false");
    }

    [Fact]
    public async Task PlanningReadFeature_WithoutFlag_SendsNoQuery()
    {
        await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        FindRequest(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}")
            .Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlanningReadFeature_WithSections_PassesTheQueryAndMirrorsTheProjection()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            Canned.PlanningFeatureSectionedJson);

        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["sections"] = "goal,log"
        });

        var request = FindRequest(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");
        ParseQuery(request.Query)["sections"].ShouldBe("goal,log");

        var feature = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        feature["body"]!.GetValue<string>().ShouldBe("<goal>\nShip the demo.\n</goal>");
        feature["missingSections"]!.ToJsonString().ShouldBe("""["log"]""");
    }

    [Fact]
    public async Task PlanningReadFeature_SectionsWithRoster_CombinesTheQuery()
    {
        await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["includeArtifacts"] = false,
            ["sections"] = "goal"
        });

        var request = FindRequest(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");
        ParseQuery(request.Query)["includeArtifacts"].ShouldBe("false");
        ParseQuery(request.Query)["sections"].ShouldBe("goal");
    }

    [Fact]
    public async Task PlanningCurrentTask_TaskState_ReturnsTheArtifactPayload()
    {
        var response = await Driver.CallToolAsync("planning_current_task", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/current");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Wire the API");
        text.ShouldContain("plan not validated");
        text.ShouldContain("\"blocked\":false");
        text.ShouldContain("\"blockedBy\":[1]");
        text.ShouldContain("\"pointers\":[]");
    }

    [Fact]
    public async Task PlanningCurrentTask_TaskBlockedStop_NamesTheUnclosedTaskChain()
    {
        var response = await Driver.CallToolAsync("planning_current_task", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningTaskBlockedSlug
        });

        var payload = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        payload.First().Key.ShouldBe("reason");
        payload["reason"]!.GetValue<string>().ShouldContain("STOP: every actionable task is blocked by unclosed tasks");
        payload["reason"]!.GetValue<string>().ShouldContain("task 3 blocked by [1, 2]");
        payload["state"]!.GetValue<string>().ShouldBe("stop");
        payload["artifact"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningCurrentTask_HeldState_LeadsWithTheReason()
    {
        var response = await Driver.CallToolAsync("planning_current_task", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningHeldSlug
        });

        var text = Canned.ToolResultText(response);
        text.ShouldContain("\"state\":\"held\"");
        text.ShouldContain("2 in review — awaiting review completion.");
        text.ShouldNotContain("\"artifact\":{");
    }

    [Fact]
    public async Task PlanningCurrentTask_StopState_LeadsWithTheReason()
    {
        var response = await Driver.CallToolAsync("planning_current_task", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningStopSlug
        });

        var payload = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        var first = payload.First();
        first.Key.ShouldBe("reason");
        first.Value!.GetValue<string>().ShouldStartWith("STOP:");
        payload["state"]!.GetValue<string>().ShouldBe("stop");
    }

    [Fact]
    public async Task PlanningCreateFeature_PostsSlugTitleAndBody()
    {
        var response = await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = "new-feature",
            ["title"] = "New feature",
            ["body"] = "<goal>\nDo the thing.\n</goal>"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/planning/features", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/features");
        var body = JsonNode.Parse(request.Body)!;
        body["slug"]!.GetValue<string>().ShouldBe("new-feature");
        body["title"]!.GetValue<string>().ShouldBe("New feature");
        body["body"]!.GetValue<string>().ShouldContain("Do the thing.");

        var text = Canned.ToolResultText(response);
        var confirmation = JsonNode.Parse(text)!.AsObject();
        confirmation["slug"]!.GetValue<string>().ShouldBe("demo-feature");
        confirmation["status"]!.GetValue<string>().ShouldBe("inProgress");
        confirmation["version"]!.GetValue<int>().ShouldBe(4);
        confirmation["bodyChars"]!.GetValue<int>().ShouldBe(Canned.StoredLength(Canned.PlanningFeatureJson, "body"));
        text.ShouldNotContain("Do the thing.");
        text.ShouldNotContain("\"artifacts\"");
    }

    [Fact]
    public async Task PlanningCreateFeature_WithPriority_PostsItAndCarriesItBack()
    {
        var response = await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = "hot-feature",
            ["title"] = "Hot feature",
            ["priority"] = "p1"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/planning/features", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["slug"]!.GetValue<string>().ShouldBe("hot-feature");
        body["priority"]!.GetValue<string>().ShouldBe("p1");

        Canned.ToolResultText(response).ShouldContain("\"priority\":\"p1\"");
    }

    [Fact]
    public async Task PlanningCreateFeature_WithoutPriority_OmitsItFromThePostedJson()
    {
        await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = "plain-feature",
            ["title"] = "Plain feature"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/planning/features", StringComparison.Ordinal));
        JsonNode.Parse(request.Body)!.AsObject().ContainsKey("priority").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningCreateFeature_WithoutSlug_OmitsItFromThePostedJson()
    {
        await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["title"] = "Generated feature"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/planning/features", StringComparison.Ordinal));
        JsonNode.Parse(request.Body)!.AsObject().ContainsKey("slug").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlanningCreateFeature_BlankSlug_OmitsItFromThePostedJson(string slug)
    {
        await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = slug,
            ["title"] = "Generated feature"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/planning/features", StringComparison.Ordinal));
        JsonNode.Parse(request.Body)!.AsObject().ContainsKey("slug").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningCreateFeature_Description_DocumentsSlugGeneration()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_create_feature"]!["description"]!.GetValue<string>();

        description.ShouldContain("slug is optional");
        description.ShouldContain("{next free NNN}-{title-derived slug}");
        description.ShouldContain("an explicit slug always wins");
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("p9")]
    public async Task PlanningCreateFeature_UnknownPriority_RejectedWithoutHttpCall(string priority)
    {
        var response = await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = "broken-feature",
            ["title"] = "Broken feature",
            ["priority"] = priority
        });

        var text = ToolErrorText(response);
        text.ShouldContain($"Invalid feature priority '{priority}'");
        text.ShouldContain("p0, p1, p2, p3");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/planning/features", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningCreateFeature_NumberCollision409_SurfacesTheTeachingDetail()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features",
            409,
            Canned.PlanningNumberCollisionJson);

        var response = await Driver.CallToolAsync("planning_create_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = "7-other",
            ["title"] = "Other"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("overmind API returned 409");
        text.ShouldContain("already taken by '075-sprint'");
        text.ShouldContain("compare numerically");
        text.ShouldContain("next free number: 076");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningCreateFeature_Description_DocumentsTheNumberGuard()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_create_feature"]!["description"]!.GetValue<string>();

        description.ShouldContain("409 NumberCollision");
        description.ShouldContain("7 = 007");
        description.ShouldContain("next free number");
    }

    [Fact]
    public async Task PlanningImportFeature_Description_DocumentsTheCreateOnlyGuard()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_import_feature"]!["description"]!.GetValue<string>();

        description.ShouldContain("never enforces the create-path number-collision guard");
        description.ShouldContain("restores verbatim");
    }

    [Fact]
    public async Task PlanningUpsertArtifact_PostsNormalizedKindWithOptions()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "# Third task\n\n<goal>More work.</goal>",
            ["order"] = 3,
            ["review"] = "hard",
            ["logNote"] = "adding a task",
            ["brief"] = "Wire the polling hook"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts");
        var body = JsonNode.Parse(request.Body)!;
        body["kind"]!.GetValue<string>().ShouldBe("task");
        body["body"]!.GetValue<string>().ShouldContain("More work.");
        body["order"]!.GetValue<int>().ShouldBe(3);
        body["review"]!.GetValue<string>().ShouldBe("hard");
        body["logNote"]!.GetValue<string>().ShouldBe("adding a task");
        body["brief"]!.GetValue<string>().ShouldBe("Wire the polling hook");
        body["source"]!.GetValue<string>().ShouldBe("agent");
        body.AsObject().Select(p => p.Key).ToArray().ShouldBe(
            ["kind", "body", "order", "review", "logNote", "brief", "source"], ignoreOrder: true);

        response["result"]!["isError"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        var confirmation = JsonNode.Parse(text)!.AsObject();
        confirmation["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        confirmation["kind"]!.GetValue<string>().ShouldBe("task");
        confirmation["status"]!.GetValue<string>().ShouldBe("draft");
        confirmation["version"]!.GetValue<int>().ShouldBe(1);
        confirmation["brief"]!.GetValue<string>().ShouldBe("Wire the polling hook");
        confirmation["bodyChars"]!.GetValue<int>().ShouldBe("# Third task\n\n<goal>More work.</goal>".Length);
        text.ShouldNotContain("More work.");
        text.ShouldNotContain("\"log\"");
    }

    [Fact]
    public async Task PlanningUpsertArtifact_MultiKilobyteBody_ConfirmationCarriesBodyCharsNotTheBody()
    {
        var marker = "UNIQUE_ECHO_MARKER_b72d91";
        var bigBody = "# Spec\n\n<goal>\n" + new string('x', 6000) + "\n" + marker + "\n</goal>";

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "spec",
            ["body"] = bigBody
        });

        var text = Canned.ToolResultText(response);
        text.ShouldNotContain(marker);
        text.ShouldNotContain("xxxx");
        text.Length.ShouldBeLessThan(500);

        var confirmation = JsonNode.Parse(text)!.AsObject();
        confirmation["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        confirmation["status"]!.GetValue<string>().ShouldBe("draft");
        confirmation["version"]!.GetValue<int>().ShouldBe(1);
        confirmation["bodyChars"]!.GetValue<int>().ShouldBe(bigBody.Length);
    }

    [Fact]
    public async Task PlanningUpsertArtifact_WithoutBrief_OmitsItFromThePostedJsonAndTheEcho()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "spec",
            ["body"] = "# Spec"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal));
        JsonNode.Parse(request.Body)!.AsObject().ContainsKey("brief").ShouldBeFalse();

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.ContainsKey("brief").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUpsertArtifact_ForwardsBlockedByOrdersInThePayload()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "# Gated task\n\n<goal>Wait for the domain model.</goal>",
            ["order"] = 3,
            ["blockedBy"] = new JsonArray(1, 2)
        });

        var body = JsonNode.Parse(Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal)).Body)!;
        body["blockedBy"]!.ToJsonString().ShouldBe("[1,2]");
        body.AsObject().Select(p => p.Key).ShouldContain("blockedBy");

        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningUpsertArtifact_EmptyBlockedBy_IsForwardedToClearEdges()
    {
        await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "# Ungated task\n\n<goal>Standalone work.</goal>",
            ["order"] = 3,
            ["blockedBy"] = new JsonArray()
        });

        var body = JsonNode.Parse(Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal)).Body)!;
        body["blockedBy"]!.ToJsonString().ShouldBe("[]");
    }

    [Fact]
    public async Task PlanningUpsertArtifact_WithoutBlockedBy_OmitsItFromThePostedJson()
    {
        await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "# Solo task\n\n<goal>No waits.</goal>",
            ["order"] = 4
        });

        var body = JsonNode.Parse(Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal)).Body)!;
        body.AsObject().ContainsKey("blockedBy").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUpsertTasks_PostsTheWholeBatchToTheTasksRouteInOneCall()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject
                {
                    ["body"] = "# Task one\n\n<goal>Model it.</goal>",
                    ["brief"] = "Model the domain"
                },
                new JsonObject
                {
                    ["body"] = "# Task two\n\n<goal>Wire it.</goal>",
                    ["order"] = 5,
                    ["review"] = "hard",
                    ["logNote"] = "imported from the plan"
                },
                new JsonObject
                {
                    ["body"] = "# Task three\n\n<goal>Ship it.</goal>"
                }
            }
        });

        var batchRequests = Stub.Requests
            .Where(r => r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal))
            .ToArray();
        batchRequests.Length.ShouldBe(1);
        batchRequests[0].Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts/tasks");

        var posted = JsonNode.Parse(batchRequests[0].Body)!["tasks"]!.AsArray();
        posted.Count.ShouldBe(3);
        posted[0]!["body"]!.GetValue<string>().ShouldContain("Model it.");
        posted[0]!.AsObject().ContainsKey("order").ShouldBeFalse();
        posted[1]!["order"]!.GetValue<int>().ShouldBe(5);
        posted[1]!["review"]!.GetValue<string>().ShouldBe("hard");
        posted[1]!["logNote"]!.GetValue<string>().ShouldBe("imported from the plan");
        posted[2]!["brief"].ShouldBeNull();

        response["result"]!["isError"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        var echo = JsonNode.Parse(text)!.AsObject();
        echo["created"]!.GetValue<int>().ShouldBe(3);
        echo["updated"]!.GetValue<int>().ShouldBe(0);
        var tasks = echo["tasks"]!.AsArray();
        tasks.Count.ShouldBe(3);
        tasks[1]!["order"]!.GetValue<int>().ShouldBe(5);
        tasks[1]!["status"]!.GetValue<string>().ShouldBe("todo");
        tasks[1]!["version"]!.GetValue<int>().ShouldBe(1);
        tasks[1]!["bodyChars"]!.GetValue<int>().ShouldBe("# Task two\n\n<goal>Wire it.</goal>".Length);
        tasks[0]!["brief"]!.GetValue<string>().ShouldBe("Model the domain");
        text.ShouldNotContain("Wire it.");
        text.ShouldNotContain("\"log\"");
    }

    [Fact]
    public async Task PlanningUpsertTasks_ItemsForwardBlockedBy_IncludingEmptyToClear()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject
                {
                    ["body"] = "# Task one\n\n<goal>Model it.</goal>",
                    ["order"] = 1,
                    ["blockedBy"] = new JsonArray(2)
                },
                new JsonObject
                {
                    ["body"] = "# Task two\n\n<goal>Wire it.</goal>",
                    ["order"] = 2,
                    ["blockedBy"] = new JsonArray()
                },
                new JsonObject
                {
                    ["body"] = "# Task three\n\n<goal>Ship it.</goal>",
                    ["order"] = 3
                }
            }
        });

        var posted = JsonNode.Parse(Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal)).Body)!["tasks"]!.AsArray();
        posted.Count.ShouldBe(3);
        posted[0]!["blockedBy"]!.ToJsonString().ShouldBe("[2]");
        posted[1]!["blockedBy"]!.ToJsonString().ShouldBe("[]");
        posted[2]!.AsObject().ContainsKey("blockedBy").ShouldBeFalse();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!["tasks"]!.AsArray();
        echo[0]!["blockedBy"]!.ToJsonString().ShouldBe("[2]");
        echo[1]!["blockedBy"]!.ToJsonString().ShouldBe("[]");
        echo[2]!["blockedBy"]!.ToJsonString().ShouldBe("[]");
    }

    [Fact]
    public async Task PlanningUpsertTasks_ArrayEncodedAsString_IsCoercedIntoTheBatch()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = """[{"body":"# Task one\n\n<goal>Model it.</goal>"}]"""
        });

        var posted = JsonNode.Parse(Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal)).Body)!["tasks"]!.AsArray();
        posted.Count.ShouldBe(1);
        posted[0]!["body"]!.GetValue<string>().ShouldContain("Model it.");

        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningUpsertTasks_NonArrayBlockedBy_RejectedInsteadOfSilentlyDropped()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject
                {
                    ["body"] = "# Task one\n\n<goal>Model it.</goal>",
                    ["order"] = 1,
                    ["blockedBy"] = 3
                }
            }
        });

        ToolErrorText(response).ShouldContain("blockedBy must be an array of task orders");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningUpsertTasks_ApiSurfacesPerItemIndexInDetail()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts/tasks",
            400,
            """{"title":"ChangeNoteRequired","detail":"tasks[1]: A logNote is required to modify a validated artifact.","status":400}""");

        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject { ["body"] = "first" },
                new JsonObject { ["body"] = "second" }
            }
        });

        var text = ToolErrorText(response);
        text.ShouldContain("tasks[1]");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUpsertTasks_BlankBodyAtItemOne_RejectedWithIndexWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject { ["body"] = "# Task one" },
                new JsonObject { ["body"] = "   " }
            }
        });

        var text = ToolErrorText(response);
        text.ShouldContain("tasks[1]");
        text.ShouldContain("body is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("medium")]
    [InlineData("")]
    public async Task PlanningUpsertTasks_UnknownReviewAtItemZero_RejectedWithIndexWithoutHttpCall(string review)
    {
        var arguments = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject { ["body"] = "# Task one", ["review"] = review },
                new JsonObject { ["body"] = "# Task two" }
            }
        };

        var response = await Driver.CallToolAsync("planning_upsert_tasks", arguments);

        var text = ToolErrorText(response);
        text.ShouldContain("tasks[0]");
        text.ShouldContain("Invalid review level");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(1e30)]
    public async Task PlanningUpsertTasks_NonIntegralOrderAtItemOne_RejectedWithIndexWithoutHttpCall(double order)
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray
            {
                new JsonObject { ["body"] = "# Task one" },
                new JsonObject { ["body"] = "# Task two", ["order"] = order }
            }
        });

        var text = ToolErrorText(response);
        text.ShouldContain("tasks[1]");
        text.ShouldContain("order must be an integer");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningUpsertTasks_EmptyOrMalformedArray_RejectedWithoutHttpCall()
    {
        var empty = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray()
        });
        ToolErrorText(empty).ShouldContain("at least one item");

        var notItems = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray("just a string")
        });
        ToolErrorText(notItems).ShouldContain("tasks[0]");
        ToolErrorText(notItems).ShouldContain("each item must be an object");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningSetStatus_PostsStatusAndLogNote()
    {
        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "review",
            ["logNote"] = "implementation finished, handing to review"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/status");
        var body = JsonNode.Parse(request.Body)!;
        body["status"]!.GetValue<string>().ShouldBe("review");
        body["logNote"]!.GetValue<string>().ShouldBe("implementation finished, handing to review");

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine"]);
        confirmation["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        confirmation["version"]!.GetValue<int>().ShouldBe(4);
        confirmation["status"]!.GetValue<string>().ShouldBe("review");
        confirmation["lastLogLine"]!.GetValue<string>().ShouldBe("status move recorded on the board");
        confirmation["lastLogLine"]!.GetValue<string>()
            .ShouldNotBe("implementation finished, handing to review");
        confirmation.ContainsKey("warnings").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningSetStatus_ApiReportsWarnings_SurfacesThemInTheConfirmation()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/status",
            200,
            """{"id":"99999999-9999-9999-9999-999999999999","status":"done","version":5,"lastLogLine":"closed with gaps","warnings":["8 of 12 acceptance items unticked"]}""");

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "done",
            ["logNote"] = "field report closed the loop"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine", "warnings"]);
        confirmation["status"]!.GetValue<string>().ShouldBe("done");
        confirmation["lastLogLine"]!.GetValue<string>().ShouldBe("closed with gaps");
        var warnings = confirmation["warnings"]!.AsArray();
        warnings.Count.ShouldBe(1);
        warnings[0]!.GetValue<string>().ShouldBe("8 of 12 acceptance items unticked");
    }

    [Fact]
    public async Task PlanningSetStatus_ApiReportsNoWarnings_OmitsTheProperty()
    {
        Stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/status", 200, Canned.PlanningArtifactStatusEchoJson);

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "done",
            ["logNote"] = "every item ticked"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.ContainsKey("warnings").ShouldBeFalse();
        confirmation.ContainsKey("featureStatusChanged").ShouldBeFalse();
        confirmation["status"]!.GetValue<string>().ShouldBe("review");
    }

    [Fact]
    public async Task PlanningSetStatus_ApiReportsFeatureDemotion_EchoesFeatureStatusChanged()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/status",
            200,
            """{"id":"99999999-9999-9999-9999-999999999999","status":"draft","version":6,"lastLogLine":"demotion recorded on the board","featureStatusChanged":"review → inProgress"}""");

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "draft",
            ["logNote"] = "review outcome: spec rejected"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine", "featureStatusChanged"]);
        confirmation["status"]!.GetValue<string>().ShouldBe("draft");
        confirmation["featureStatusChanged"]!.GetValue<string>().ShouldBe("review → inProgress");
    }

    [Fact]
    public async Task PlanningCurrentTask_BlockedFeature_StopStateLeadsWithReasonNamingUnclosedBlockers()
    {
        var response = await Driver.CallToolAsync("planning_current_task", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningBlockedSlug
        });

        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningBlockedSlug}/current");

        var payload = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        var first = payload.First();
        first.Key.ShouldBe("reason");
        var reason = first.Value!.GetValue<string>();
        reason.ShouldStartWith("STOP:");
        reason.ShouldContain("is blocked by unclosed features");
        reason.ShouldContain("gate-locked (ready)");
        reason.ShouldContain("draft-one (draft)");
        reason.ShouldContain("Reads stay open");
        payload["state"]!.GetValue<string>().ShouldBe("stop");
        payload["artifact"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningListFeatureDependencies_ProxiesAgentDependenciesRoute()
    {
        var response = await Driver.CallToolAsync("planning_list_feature_dependencies", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var request = FindRequest(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/dependencies");
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/dependencies");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("\"slug\":\"demo-feature\"");
        text.ShouldContain("\"slug\":\"gate-locked\"");
        text.ShouldContain("\"closed\":false");
        text.ShouldContain("\"blocks\"");
    }

    [Fact]
    public async Task PlanningAddFeatureDependency_PostsBlockedBySlug()
    {
        var response = await Driver.CallToolAsync("planning_add_feature_dependency", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["blockedBySlug"] = "gate-locked"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/dependencies", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/dependencies");
        var body = JsonNode.Parse(request.Body)!;
        body["blockedBy"]!.GetValue<string>().ShouldBe("gate-locked");
        body.AsObject().Count.ShouldBe(1);

        Canned.ToolResultText(response).ShouldContain("\"slug\":\"gate-locked\"");
    }

    [Fact]
    public async Task PlanningAddFeatureDependency_CycleSurfacesAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/dependencies",
            409,
            """{"title":"DependencyCycle","detail":"Adding 'gate-locked' as a blocker of 'demo-feature' would create a dependency cycle: demo-feature -> gate-locked -> demo-feature.","status":409}""");

        var response = await Driver.CallToolAsync("planning_add_feature_dependency", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["blockedBySlug"] = "gate-locked"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("dependency cycle");
        text.ShouldContain("demo-feature -> gate-locked -> demo-feature");
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public async Task PlanningAddFeatureDependency_BlankBlocker_RejectedWithoutHttpCall(string blockedBy)
    {
        var response = await Driver.CallToolAsync("planning_add_feature_dependency", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["blockedBySlug"] = blockedBy
        });

        ToolErrorText(response).ShouldContain("blockedBySlug is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/dependencies", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningRemoveFeatureDependency_SendsDeleteWithTheBlockerInThePath()
    {
        var response = await Driver.CallToolAsync("planning_remove_feature_dependency", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["blockedBySlug"] = "gate-locked"
        });

        var request = Stub.Requests.Single(r => r.Method == "DELETE");
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/dependencies/gate-locked");
        request.Body.ShouldBeEmpty();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("\"slug\":\"demo-feature\"");
        text.ShouldNotContain("\"slug\":\"gate-locked\"");
    }

    [Fact]
    public async Task PlanningRemoveFeatureDependency_BlankBlocker_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_remove_feature_dependency", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["blockedBySlug"] = " "
        });

        ToolErrorText(response).ShouldContain("blockedBySlug is required");
        Stub.Requests.ShouldNotContain(r => r.Method == "DELETE");
    }

    [Fact]
    public async Task PlanningSetFeatureStatus_PostsStatusAndLogNoteToAgentRoute()
    {
        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["status"] = "done",
            ["logNote"] = "by user request in-session"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal) &&
            r.Path.Contains("/planning/features/", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/status");
        var body = JsonNode.Parse(request.Body)!;
        body["status"]!.GetValue<string>().ShouldBe("done");
        body["logNote"]!.GetValue<string>().ShouldBe("by user request in-session");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine"]);
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        echo["version"]!.GetValue<int>().ShouldBe(5);
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("feature mutation recorded on the board");
    }

    [Fact]
    public async Task PlanningSetFeatureStatus_EmptyLogNote_RejectedClientSide()
    {
        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["status"] = "done",
            ["logNote"] = ""
        });

        var text = ToolErrorText(response);
        text.ShouldContain("logNote is required");
        text.ShouldContain("feature");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/planning/features/" + Canned.PlanningSlug + "/status", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("ready")]
    [InlineData("review")]
    [InlineData("banana")]
    public async Task PlanningSetFeatureStatus_NonAgentTarget_RejectedWithoutHttpCall(string status)
    {
        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["status"] = status,
            ["logNote"] = "by user request in-session"
        });

        var text = ToolErrorText(response);
        text.ShouldContain($"Invalid feature status '{status}'");
        text.ShouldContain("done");
        text.ShouldContain("inProgress");
        text.ShouldContain("human-door");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/planning/features/" + Canned.PlanningSlug + "/status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningSetFeatureStatus_InvalidTransition_SurfacesTheCurrentStatus()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/status",
            400,
            """{"title":"InvalidFeatureTransition","detail":"Feature 'demo-feature' cannot move from 'done' to 'inProgress'; allowed targets: .","status":400}""");

        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["status"] = "inProgress",
            ["logNote"] = "by user request in-session"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("cannot move from 'done'");
        text.ShouldContain("allowed targets");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUpdateFeature_Description_PinsThePartialEditPitch()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_update_feature"]!["description"]!.GetValue<string>().ShouldBe(
            "Partial edit of a feature's title, body, priority, lane, or codeSurfaces — durable what/why stays correctable by agents. " +
            "logNote is mandatory and lands as a dated line in the feature log. " +
            "A priority change moves the feature to the tail of its priority band. " +
            "lane assigns the feature to a lane roster entry by name — an unknown name auto-creates the lane row " +
            "(empty contour, no priority): the assigning act is the birth, planning_set_lanes cannot create lanes; " +
            "empty string clears the lane, omit to keep. " +
            "A lane whose last open feature leaves or closes retires itself. " +
            "codeSurfaces declares the repo-relative glob patterns this feature touches (e.g. src/web/**) — advisory only: " +
            "the queue, board and wave-snapshot rows project potentialOverlaps between in-flight features so coordinators " +
            "see cross-lane collisions; an empty array clears the surfaces, omit to keep. " +
            "Status is not editable here — use planning_set_feature_status.");
    }

    [Fact]
    public async Task PlanningSetLanes_Description_TeachesTheUpsertShift()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_set_lanes"]!["description"]!.GetValue<string>();

        description.ShouldContain("UPSERT of known lanes");
        description.ShouldContain("lanes absent from the payload are never touched");
        description.ShouldContain("An empty array is an explicit no-op");
        description.ShouldContain("the birth doors are planning_create_lane and assignment via planning_update_feature's lane field");
        description.ShouldContain("planning_remove_lane");
        description.ShouldNotContain("only birth path");
        description.ShouldNotContain("full replace");
        description.ShouldNotContain("deleted");
        description.ShouldNotContain("wholesale");
        description.ShouldNotContain("clears the roster");
    }

    [Fact]
    public async Task PlanningClaimFeature_Description_BlocksOnlyOnAliveForeignClaims()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_claim_feature"]!["description"]!.GetValue<string>();

        description.ShouldContain("ALIVE claim by a different alias+vendor pair");
        description.ShouldContain("unclaimed open features in the lane and the caller's own claims never block");
        description.ShouldNotContain("serial");
    }

    [Fact]
    public async Task PlanningRemoveLane_Description_NamesTheRemovalContract()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_remove_lane"]!["description"]!.GetValue<string>();

        description.ShouldContain("the only manual removal path");
        description.ShouldContain("409 LaneStillOccupied");
        description.ShouldContain("idempotent no-op");
    }

    [Fact]
    public async Task PlanningCreateLane_Description_TeachesTheBirthDoor()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_create_lane"]!["description"]!.GetValue<string>();

        description.ShouldContain("planning_set_lanes refuses");
        description.ShouldContain("idempotent by echo");
        description.ShouldContain("case-insensitive");
        description.ShouldContain("409 LaneNameDuplicate");
        description.ShouldContain("LaneRosterTooLarge");
        description.ShouldContain("No claim lock is involved");
    }

    [Fact]
    public async Task PlanningCreateLane_Schema_MakesOnlyNameRequired()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_create_lane")!;

        var required = tool["inputSchema"]!["required"]!.AsArray()
            .Select(r => r!.GetValue<string>())
            .ToArray();
        required.ShouldBe(["name"]);

        var properties = tool["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("contour");
        properties.ShouldContainKey("priority");
        properties.ShouldContainKey("boundAlias");
        properties.ShouldContainKey("projectId");
    }

    [Fact]
    public async Task PlanningUpdateFeature_Schema_MakesLogNoteRequired()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_update_feature")!;

        var required = tool["inputSchema"]!["required"]!.AsArray()
            .Select(r => r!.GetValue<string>())
            .ToArray();
        required.ShouldNotContain("projectId");
        required.ShouldContain("slug");
        required.ShouldContain("logNote");
        required.ShouldNotContain("title");
        required.ShouldNotContain("body");
        required.ShouldNotContain("priority");
        required.ShouldNotContain("codeSurfaces");

        var properties = tool["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("projectId");
        properties.ShouldContainKey("slug");
        properties.ShouldContainKey("logNote");
        properties.ShouldContainKey("title");
        properties.ShouldContainKey("body");
        properties.ShouldContainKey("priority");
        properties.ShouldContainKey("codeSurfaces");
    }

    [Fact]
    public async Task PlanningUpdateFeature_PostsLogNoteAndSetFields_PassesEchoThrough()
    {
        var response = await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "fixing the typo the field agent could not",
            ["title"] = "Correct title",
            ["body"] = "<goal>\nFixed.\n</goal>"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/update", StringComparison.Ordinal) &&
            r.Path.Contains("/planning/features/", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/update");
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body["logNote"]!.GetValue<string>().ShouldBe("fixing the typo the field agent could not");
        body["title"]!.GetValue<string>().ShouldBe("Correct title");
        body["body"]!.GetValue<string>().ShouldBe("<goal>\nFixed.\n</goal>");
        body.ContainsKey("priority").ShouldBeFalse();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!;
        JsonNode.DeepEquals(echo, JsonNode.Parse(Canned.PlanningFeatureUpdateEchoJson)).ShouldBeTrue();
    }

    [Fact]
    public async Task PlanningUpdateFeature_PostsCodeSurfacesArray_PassesThroughIncludingEmptyClear()
    {
        await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "declaring surfaces",
            ["codeSurfaces"] = new JsonArray("src/web/**", "src/api/**/*.cs")
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/update", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body["codeSurfaces"]!.AsArray().Select(v => v!.GetValue<string>())
            .ShouldBe(["src/web/**", "src/api/**/*.cs"]);

        await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "clearing surfaces",
            ["codeSurfaces"] = new JsonArray()
        });

        var clear = Stub.Requests.Last(r =>
            r.Method == "POST" && r.Path.EndsWith("/update", StringComparison.Ordinal));
        JsonNode.Parse(clear.Body)!["codeSurfaces"]!.AsArray().Count.ShouldBe(0);
    }

    [Fact]
    public async Task PlanningUpdateFeature_WithoutOptionalFields_SendsLogNoteOnly()
    {
        await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "no-op edit probe"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/update", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body.Count.ShouldBe(1);
        body["logNote"]!.GetValue<string>().ShouldBe("no-op edit probe");
    }

    [Fact]
    public async Task PlanningUpdateFeature_BlankOptionalFields_OmittedFromPayload()
    {
        await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "blank-field probe",
            ["title"] = "   ",
            ["body"] = "  \n\t",
            ["priority"] = " "
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/update", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body.Count.ShouldBe(1);
        body.ContainsKey("title").ShouldBeFalse();
        body.ContainsKey("body").ShouldBeFalse();
        body.ContainsKey("priority").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUpdateFeature_BadPriority_SurfacesApiErrorAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/update",
            400,
            """{"title":"InvalidFeaturePriority","detail":"Invalid feature priority 'p9'. Expected one of: p0, p1, p2, p3.","status":400}""");

        var response = await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["priority"] = "p9",
            ["logNote"] = "priority probe"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Invalid feature priority 'p9'");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUpdateFeature_UnknownFeature_SurfacesApiErrorAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/update",
            404,
            Canned.PlanningFeatureNotFoundJson);

        var response = await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["title"] = "Renamed",
            ["logNote"] = "rename probe"
        });

        ToolErrorText(response).ShouldContain("Feature 'demo-feature' was not found.");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPatchFeature_Description_PinsTheSectionPatchPitch()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_patch_feature"]!["description"]!.GetValue<string>();

        description.ShouldContain("without resending the body");
        description.ShouldContain("INSERTED — with the content, or declared-empty ('(none)') when content is omitted");
        description.ShouldContain("no heading mode");
        description.ShouldContain("planning_patch_artifact");
        description.ShouldContain("'<removed YYYY-MM-DD — reason>'");
        description.ShouldContain("logNote is mandatory");
        description.ShouldContain("claim-locked");
    }

    [Fact]
    public async Task PlanningPatchFeature_Schema_MakesSectionAndLogNoteRequired()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_patch_feature")!;

        var required = tool["inputSchema"]!["required"]!.AsArray()
            .Select(r => r!.GetValue<string>())
            .ToArray();
        required.ShouldNotContain("projectId");
        required.ShouldContain("slug");
        required.ShouldContain("section");
        required.ShouldContain("logNote");
        required.ShouldNotContain("content");
        required.ShouldNotContain("reason");
        required.ShouldNotContain("remove");

        var properties = tool["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("projectId");
        properties.ShouldContainKey("slug");
        properties.ShouldContainKey("section");
        properties.ShouldContainKey("logNote");
        properties.ShouldContainKey("content");
        properties.ShouldContainKey("reason");
        properties.ShouldContainKey("remove");
    }

    [Fact]
    public async Task PlanningPatchFeature_PostsSectionAndContent_PassesEchoThrough()
    {
        var response = await Driver.CallToolAsync("planning_patch_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["section"] = "goal",
            ["content"] = "Ship the demo faster.",
            ["logNote"] = "goal sharpened"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal) &&
            r.Path.Contains("/planning/features/", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/patch");
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body["section"]!.GetValue<string>().ShouldBe("goal");
        body["content"]!.GetValue<string>().ShouldBe("Ship the demo faster.");
        body["logNote"]!.GetValue<string>().ShouldBe("goal sharpened");
        body["remove"]!.GetValue<bool>().ShouldBeFalse();
        body.ContainsKey("reason").ShouldBeFalse();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!;
        echo["slug"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        echo["section"]!.GetValue<string>().ShouldBe("goal");
        echo["version"]!.GetValue<int>().ShouldBe(7);
        echo["bodyChars"]!.GetValue<int>().ShouldBe(480);
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("feature patch recorded on the board");
    }

    [Fact]
    public async Task PlanningPatchFeature_PostsRemoveWithReason_OmitsContent()
    {
        await Driver.CallToolAsync("planning_patch_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["section"] = "refs",
            ["remove"] = true,
            ["reason"] = "folded into the spec",
            ["logNote"] = "tombstoned refs"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body["section"]!.GetValue<string>().ShouldBe("refs");
        body["remove"]!.GetValue<bool>().ShouldBeTrue();
        body["reason"]!.GetValue<string>().ShouldBe("folded into the spec");
        body.ContainsKey("content").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPatchFeature_BlankLogNote_RejectedClientSide()
    {
        var response = await Driver.CallToolAsync("planning_patch_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["section"] = "goal",
            ["logNote"] = ""
        });

        var text = ToolErrorText(response);
        text.ShouldContain("logNote is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/planning/features/" + Canned.PlanningSlug + "/patch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningPatchFeature_UnknownFeature_SurfacesApiErrorAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/patch",
            404,
            Canned.PlanningFeatureNotFoundJson);

        var response = await Driver.CallToolAsync("planning_patch_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["section"] = "goal",
            ["content"] = "text",
            ["logNote"] = "patch probe"
        });

        ToolErrorText(response).ShouldContain("Feature 'demo-feature' was not found.");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningTaskQueue_ReadsTheAgentQueueRoute()
    {
        var response = await Driver.CallToolAsync("planning_task_queue", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/task-queue");
        request.Query.ShouldBeEmpty();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("\"slug\":\"demo-feature\"");
        text.ShouldContain("\"slug\":\"draft-one\"");
        text.ShouldContain("\"queuePosition\":2");
        text.ShouldContain("\"blockedBy\":[\"demo-feature\"]");
        text.ShouldContain("\"readiness\":2");

        var rows = JsonNode.Parse(text)!["items"]!.AsArray();
        var claimed = rows.Single(r => r!["slug"]!.GetValue<string>() == "demo-feature")!;
        claimed["phase"]!.GetValue<string>().ShouldBe("in progress");
        claimed["briefExcerpt"]!.GetValue<string?>().ShouldBe("Crash on save when the workspace file is read-only.");
        claimed["createdAt"]!.GetValue<string>().ShouldBe("2026-09-08T09:00:00Z");
        claimed["updatedAt"]!.GetValue<string>().ShouldBe("2026-09-10T08:00:00Z");
        var draft = rows.Single(r => r!["slug"]!.GetValue<string>() == "draft-one")!;
        draft["phase"]!.GetValue<string>().ShouldBe("drafting");
        draft["briefExcerpt"].ShouldBeNull();
        draft["createdAt"]!.GetValue<string>().ShouldBe("2026-09-09T09:00:00Z");
        draft["updatedAt"]!.GetValue<string>().ShouldBe("2026-09-09T09:00:00Z");

        claimed["taskCounts"]!.ToJsonString()
            .ShouldBe("{\"todo\":0,\"inProgress\":1,\"review\":0,\"done\":1,\"blocked\":0}");
        claimed["taskCounts"]!.AsObject().Select(p => p.Value!.GetValue<int>()).Sum()
            .ShouldBe(claimed["taskTotal"]!.GetValue<int>());
        draft["taskCounts"]!.AsObject().Select(p => p.Value!.GetValue<int>()).Sum()
            .ShouldBe(draft["taskTotal"]!.GetValue<int>());
    }

    [Fact]
    public async Task PlanningTaskQueue_LimitTruncatesThroughTheQuery()
    {
        var response = await Driver.CallToolAsync("planning_task_queue", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["limit"] = 2
        });

        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/task-queue")
            .Query.ShouldBe("limit=2");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("\"slug\":\"demo-feature\"");
        text.ShouldNotContain("\"slug\":\"draft-one\"");
    }

    [Theory]
    [InlineData("up", null)]
    [InlineData("down", null)]
    [InlineData("first", null)]
    [InlineData("last", null)]
    [InlineData("toPosition", 3)]
    public async Task PlanningMoveFeature_PostsTheGestureToTheAgentRoute(string move, int? position)
    {
        var arguments = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["move"] = move
        };
        if (position is not null)
        {
            arguments["position"] = position.Value;
        }

        var response = await Driver.CallToolAsync("planning_move_feature", arguments);

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/queue-move", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/queue-move");
        var body = JsonNode.Parse(request.Body)!;
        body["move"]!.GetValue<string>().ShouldBe(move.ToLowerInvariant());
        var positionNode = body["position"];
        if (position is null)
        {
            positionNode.ShouldBeNull();
        }
        else
        {
            positionNode!.GetValue<int>().ShouldBe(position.Value);
        }

        Canned.ToolResultText(response).ShouldContain("queue position 2 in band 'p1'");
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("backward")]
    [InlineData("TO_THE_TOP")]
    public async Task PlanningMoveFeature_UnknownMove_NamesTheContractWithoutHttpCall(string move)
    {
        var response = await Driver.CallToolAsync("planning_move_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["move"] = move
        });

        var text = ToolErrorText(response);
        text.ShouldContain($"Invalid queue move '{move}'");
        text.ShouldContain("up, down, first, last, toPosition");
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/queue-move", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningMoveFeature_ToPositionWithoutPosition_NamesTheContractWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_move_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["move"] = "toPosition"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("position is required when move is 'toPosition'");
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/queue-move", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("first")]
    [InlineData("last")]
    public async Task PlanningMoveFeature_PositionOutsideToPosition_NamesTheContractWithoutHttpCall(string move)
    {
        var response = await Driver.CallToolAsync("planning_move_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["move"] = move,
            ["position"] = 2
        });

        var text = ToolErrorText(response);
        text.ShouldContain("position is only allowed when move is 'toPosition'");
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/queue-move", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningMoveFeature_NonPositivePosition_NamesTheContractWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_move_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["move"] = "toPosition",
            ["position"] = 0
        });

        var text = ToolErrorText(response);
        text.ShouldContain("position must be a positive int (1-based)");
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/queue-move", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningMoveFeature_OutOfRangePosition_SurfacesTheEndpointContractText()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/queue-move",
            400,
            Canned.PlanningQueueOutOfRangeJson);

        var response = await Driver.CallToolAsync("planning_move_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["move"] = "toPosition",
            ["position"] = 9
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Position 9 is outside the queue band of 'demo-feature'");
        text.ShouldContain("the band holds 3 non-done features, so the valid range is 1..3.");
        text.ShouldNotContain("One or more validation errors");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningClaimFeature_PostsIdentityToTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("planning_claim_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["alias"] = "  Dan  ",
            ["vendor"] = "qwen"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/claim", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/claim");
        var body = JsonNode.Parse(request.Body)!;
        body["alias"]!.GetValue<string>().ShouldBe("Dan");
        body["vendor"]!.GetValue<string>().ShouldBe("qwen");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["slug", "claimedByAlias", "claimedByVendor", "claimedAt", "claimState"]);
        echo["claimedByAlias"]!.GetValue<string>().ShouldBe("Dan");
        echo["claimState"]!.GetValue<string>().ShouldBe("alive");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningClaimFeature_EchoesLiveSurfaces_WhenTheLaneWatchesMatchingPaths()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/claim",
            200,
            """
            {"slug":"demo-feature","claimedByAlias":"Dan","claimedByVendor":"qwen","claimedAt":"2026-10-02T10:00:00Z","claimState":"alive","lane":"web-boundary","liveSurfaces":[{"glob":"web/src/**","owner":"van-darkholme"}]}
            """);

        var response = await Driver.CallToolAsync("planning_claim_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["alias"] = "Dan",
            ["vendor"] = "qwen"
        });

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.Select(p => p.Key).ShouldBe(
            ["slug", "claimedByAlias", "claimedByVendor", "claimedAt", "claimState", "lane", "liveSurfaces"]);
        var live = echo["liveSurfaces"]!.AsArray().Single()!.AsObject();
        live["glob"]!.GetValue<string>().ShouldBe("web/src/**");
        live["owner"]!.GetValue<string>().ShouldBe("van-darkholme");
    }

    [Theory]
    [InlineData("alias", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("vendor", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task PlanningClaimFeature_IdentityOverCap_RejectedWithoutHttpCall(
        string field, string value)
    {
        var call = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["alias"] = "Dan",
            ["vendor"] = "qwen"
        };
        call[field] = value;

        var response = await Driver.CallToolAsync("planning_claim_feature", call);

        var text = ToolErrorText(response);
        text.ShouldContain("exceeds the 32-character cap");
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/claim", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningClaimFeature_Schema_MakesIdentityOptional()
    {
        var response = await Driver.ListToolsAsync();

        var tool = response["result"]!["tools"]!.AsArray()
            .Single(t => t!["name"]!.GetValue<string>() == "planning_claim_feature")!;

        var required = tool["inputSchema"]!["required"]!.AsArray()
            .Select(r => r!.GetValue<string>())
            .ToArray();
        required.ShouldContain("slug");
        required.ShouldNotContain("alias");
        required.ShouldNotContain("vendor");
        required.ShouldNotContain("projectId");

        var properties = tool["inputSchema"]!["properties"]!.AsObject();
        properties.ShouldContainKey("slug");
        properties.ShouldContainKey("alias");
        properties.ShouldContainKey("vendor");
        properties.ShouldContainKey("overrideLaneCheck");
        properties.ShouldContainKey("projectId");
    }

    [Fact]
    public async Task PlanningClaimFeature_ZeroIdentity_OmitsIdentityFromThePayload()
    {
        var response = await Driver.CallToolAsync("planning_claim_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/claim", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body.ContainsKey("alias").ShouldBeFalse();
        body.ContainsKey("vendor").ShouldBeFalse();
        body["overrideLaneCheck"]!.GetValue<bool>().ShouldBeFalse();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["claimedByAlias"]!.GetValue<string>().ShouldBe("Dan");
        echo["claimState"]!.GetValue<string>().ShouldBe("alive");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningClaimFeature_BlankIdentity_TreatedAsOmitted()
    {
        var response = await Driver.CallToolAsync("planning_claim_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["alias"] = "   ",
            ["vendor"] = ""
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/claim", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body.ContainsKey("alias").ShouldBeFalse();
        body.ContainsKey("vendor").ShouldBeFalse();
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningClaimFeature_HeldByLiveForeignSession_NamesTheHolder()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/claim",
            409,
            Canned.PlanningClaimHeldJson);

        var response = await Driver.CallToolAsync("planning_claim_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["alias"] = "Bobby",
            ["vendor"] = "claude"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Feature 'demo-feature': claim held by Dan by qwen (alive)");
        text.ShouldContain("the human door in the Overmind UI is never locked");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningUnclaimFeature_PostsToTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("planning_unclaim_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/unclaim", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/unclaim");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["claimedByAlias"]!.ShouldBeNull();
        echo["claimState"]!.ShouldBeNull();
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningSetFeaturePriority_PostsPriorityThroughTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("planning_set_feature_priority", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["priority"] = "P0"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/priority", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/priority");
        var body = JsonNode.Parse(request.Body)!;
        body["priority"]!.GetValue<string>().ShouldBe("p0");
        body.AsObject().Count.ShouldBe(1);

        var payload = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        payload["priority"]!.GetValue<string>().ShouldBe("p0");
        payload["version"]!.GetValue<int>().ShouldBe(5);
        payload["bodyChars"]!.GetValue<int>().ShouldBeGreaterThan(0);
        payload.ShouldNotContainKey("body");
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("urgent")]
    [InlineData("p4")]
    public async Task PlanningSetFeaturePriority_InvalidPriority_RejectedWithoutHttpCall(string priority)
    {
        var response = await Driver.CallToolAsync("planning_set_feature_priority", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["priority"] = priority
        });

        ToolErrorText(response).ShouldContain($"Invalid feature priority '{priority}'");
        Stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/priority", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPinFeatureCode_PostsFullReplacePayloadThroughTheAgentDoor()
    {
        var response = await Driver.CallToolAsync("planning_pin_feature_code", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["branch"] = "feat/pinned-branch",
            ["worktreePath"] = "/repo/wt"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/pin-code", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/pin-code");
        var body = JsonNode.Parse(request.Body)!;
        body["branch"]!.GetValue<string>().ShouldBe("feat/pinned-branch");
        body["worktreePath"]!.GetValue<string>().ShouldBe("/repo/wt");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["slug"]!.GetValue<string>().ShouldBe("demo-feature");
        echo["pinnedBranch"]!.GetValue<string>().ShouldBe("feat/pinned-branch");
        echo["pinnedWorktreePath"]!.GetValue<string>().ShouldBe("/repo/wt");
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("code pin set on the board");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPinFeatureCode_BlankArgsNormalizeToNulls_AndEchoTheClear()
    {
        var response = await Driver.CallToolAsync("planning_pin_feature_code", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var request = Stub.Requests.Single(r => r.Method == "POST"
            && r.Path.EndsWith("/pin-code", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["branch"].ShouldBeNull();
        body["worktreePath"].ShouldBeNull();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["pinnedBranch"].ShouldBeNull();
        echo["pinnedWorktreePath"].ShouldBeNull();
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPinFeatureCode_MissingFeature_PassesThe404Through()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/pin-code",
            404,
            Canned.PlanningFeatureNotFoundJson);

        var response = await Driver.CallToolAsync("planning_pin_feature_code", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = "ghost-feature"
        });

        ToolErrorText(response).ShouldContain("was not found");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningAppendLog_PostsTheNote()
    {
        var response = await Driver.CallToolAsync("planning_append_log", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["note"] = "halfway through the wiring"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/log", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/log");
        var posted = JsonNode.Parse(request.Body)!;
        posted["note"]!.GetValue<string>().ShouldBe("halfway through the wiring");
        posted["source"]!.GetValue<string>().ShouldBe("agent");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine"]);
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        echo["version"]!.GetValue<int>().ShouldBe(3);
        echo["status"]!.GetValue<string>().ShouldBe("inProgress");
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("log line recorded on the board");
    }

    [Fact]
    public async Task PlanningPatchArtifact_ReplacesSection_PostsOpSectionAndContent()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "goal",
            ["content"] = "<goal>\nRewired for real.\n</goal>",
            ["logNote"] = "rewiring the goal"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/patch");
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("replace_section");
        body["section"]!.GetValue<string>().ShouldBe("goal");
        body["content"]!.GetValue<string>().ShouldContain("Rewired for real.");
        body["logNote"]!.GetValue<string>().ShouldBe("rewiring the goal");
        body["source"]!.GetValue<string>().ShouldBe("agent");

        response["result"]!["isError"].ShouldBeNull();
        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine", "section", "bodyChars"]);
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        echo["version"]!.GetValue<int>().ShouldBe(3);
        echo["status"]!.GetValue<string>().ShouldBe("inProgress");
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("patch recorded on the board");
        echo["section"]!.GetValue<string>().ShouldBe("goal");
        echo["bodyChars"]!.GetValue<int>().ShouldBe(412);
    }

    [Fact]
    public async Task PlanningPatchArtifact_SectionWithoutContent_PostsReplaceSectionWithoutContent()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "obstacles"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("replace_section");
        body["section"]!.GetValue<string>().ShouldBe("obstacles");
        body["content"].ShouldBeNull();

        response["result"]!["isError"].ShouldBeNull();
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPatchArtifact_TicksByIndex_PostsTickOp()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["acIndex"] = 1
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("tick_ac");
        body["acIndex"]!.GetValue<int>().ShouldBe(1);

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["section"]!.GetValue<string>().ShouldBe("acceptance-criteria");
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("patch recorded on the board");
        echo["tickedIndex"]!.GetValue<int>().ShouldBe(1);
        echo["tickedText"]!.GetValue<string>().ShouldBe("expose the endpoints");
    }

    [Fact]
    public async Task PlanningPatchArtifact_UnticksByText_PostsUntickOp()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["acText"] = "Expose the endpoints.",
            ["untick"] = true
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("untick_ac");
        body["acText"]!.GetValue<string>().ShouldBe("Expose the endpoints.");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["section"]!.GetValue<string>().ShouldBe("acceptance-criteria");
        echo["tickedIndex"]!.GetValue<int>().ShouldBe(1);
        echo["tickedText"]!.GetValue<string>().ShouldBe("expose the endpoints");
        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningPatchArtifact_AlreadyUnticked_RendersTheHonestNoOpText()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            200,
            """{"artifactId":"99999999-9999-9999-9999-999999999999","kind":"task","section":"acceptance-criteria","version":2,"bodyChars":380,"status":"inProgress","lastLogLine":"patch recorded on the board","tickedIndex":2,"tickedText":"still open","noChange":true}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["acIndex"] = 2,
            ["untick"] = true
        });

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldBe("already unticked — no change");
    }

    [Fact]
    public async Task PlanningPatchArtifact_AmendsResult_PostsAmendResultOp()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            200,
            """{"artifactId":"99999999-9999-9999-9999-999999999999","kind":"result","section":"amendment","version":2,"bodyChars":380,"status":"validated","lastLogLine":"human pre-approved the correction"}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["amend"] = true,
            ["content"] = "totals corrected to 42h\nSupersedes: the 41h figure",
            ["logNote"] = "human pre-approved the correction"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("amend_result");
        body["section"].ShouldBeNull();
        body["content"]!.GetValue<string>().ShouldContain("totals corrected to 42h");
        body["logNote"]!.GetValue<string>().ShouldBe("human pre-approved the correction");
        body["source"]!.GetValue<string>().ShouldBe("agent");

        response["result"]!["isError"].ShouldBeNull();
        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["section"]!.GetValue<string>().ShouldBe("amendment");
        echo["status"]!.GetValue<string>().ShouldBe("validated");
        echo["lastLogLine"]!.GetValue<string>().ShouldBe("human pre-approved the correction");
    }

    [Fact]
    public async Task PlanningPatchArtifact_AmendWithSectionOrBlankContent_RejectedWithoutHttpCall()
    {
        var withSection = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["amend"] = true,
            ["section"] = "goal",
            ["content"] = "text"
        });

        ToolErrorText(withSection).ShouldContain("never several and never none");

        var blankContent = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["amend"] = true,
            ["content"] = " "
        });

        ToolErrorText(blankContent).ShouldContain("non-blank content");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningPatchArtifact_HeadingPatch_PostsTheHeadingAddressVerbatim()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString(),
            ["section"] = "## Scope",
            ["content"] = "The trimmed scope.",
            ["logNote"] = "scope rewrite"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("replace_section");
        body["section"]!.GetValue<string>().ShouldBe("## Scope");
        body["content"]!.GetValue<string>().ShouldBe("The trimmed scope.");
        body["source"]!.GetValue<string>().ShouldBe("agent");

        response["result"]!["isError"].ShouldBeNull();
        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["section"]!.GetValue<string>().ShouldBe("## Scope");
    }

    [Fact]
    public async Task PlanningPatchArtifact_Tombstone_PostsRemoveSectionWithReason()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString(),
            ["section"] = "## Scope",
            ["remove"] = true,
            ["reason"] = "scope cut in review",
            ["logNote"] = "tombstoning the scope"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("remove_section");
        body["section"]!.GetValue<string>().ShouldBe("## Scope");
        body["reason"]!.GetValue<string>().ShouldBe("scope cut in review");
        body["content"].ShouldBeNull();
        request.Body.ShouldNotContain("\"remove\"");
        body["logNote"]!.GetValue<string>().ShouldBe("tombstoning the scope");

        response["result"]!["isError"].ShouldBeNull();
        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["section"]!.GetValue<string>().ShouldBe("## Scope");
    }

    [Fact]
    public async Task PlanningPatchArtifact_RemoveWithoutReason_RejectedBeforeTheCall()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString(),
            ["section"] = "## Scope",
            ["remove"] = true
        });

        var text = ToolErrorText(response);
        text.ShouldContain("remove=true needs reason");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningPatchArtifact_DuplicateHeading_SurfacesTheAmbiguity400()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            400,
            """{"title":"PatchHeadingAmbiguous","detail":"Heading '## Scope' matches 2 H2 headings in the body ('## Scope', '## Scope'); a patch would rewrite an arbitrary one — nothing was modified. Merge or rename the duplicates first.","status":400}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString(),
            ["section"] = "## Scope",
            ["content"] = "The trimmed scope."
        });

        var text = ToolErrorText(response);
        text.ShouldContain("matches 2 H2 headings");
        text.ShouldContain("nothing was modified");
    }

    [Fact]
    public async Task PlanningPatchArtifact_TicksByText_PostsTheExactText()
    {
        await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["acText"] = "Expose the endpoints."
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        var body = JsonNode.Parse(request.Body)!;
        body["op"]!.GetValue<string>().ShouldBe("tick_ac");
        body["acText"]!.GetValue<string>().ShouldBe("Expose the endpoints.");
        body["acIndex"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningPatchArtifact_EchoCarriesTheProjectionNotTheBody()
    {
        var marker = "UNIQUE_PATCH_MARKER_c31d77";
        var content = "<goal>\n" + new string('y', 4000) + marker + "\n</goal>";

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "goal",
            ["content"] = content
        });

        var text = Canned.ToolResultText(response);
        text.ShouldNotContain(marker);
        text.ShouldNotContain("yyyy");
        text.Length.ShouldBeLessThan(300);

        var echo = JsonNode.Parse(text)!.AsObject();
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        echo["version"]!.GetValue<int>().ShouldBe(3);
        echo["bodyChars"]!.GetValue<int>().ShouldBe(412);
    }

    [Fact]
    public async Task PlanningPatchArtifact_Demotion_EchoesStatusChanged()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            200,
            """{"artifactId":"99999999-9999-9999-9999-999999999999","kind":"spec","section":"goal","version":3,"bodyChars":97,"status":"draft","lastLogLine":"reopened for rework","statusChanged":"validated → draft","featureStatusChanged":"review → inProgress"}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "goal",
            ["content"] = "<goal>\nReworked after review.\n</goal>",
            ["logNote"] = "reopened for rework"
        });

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine", "section", "bodyChars", "statusChanged", "featureStatusChanged"]);
        echo["version"]!.GetValue<int>().ShouldBe(3);
        echo["status"]!.GetValue<string>().ShouldBe("draft");
        echo["statusChanged"]!.GetValue<string>().ShouldBe("validated → draft");
        echo["featureStatusChanged"]!.GetValue<string>().ShouldBe("review → inProgress");
    }

    [Fact]
    public async Task PlanningPatchArtifact_Retick_RendersTheHonestNoOpText()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            200,
            Canned.PlanningPatchRetickEchoJson);

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["acIndex"] = 1
        });

        response["result"]!["isError"].ShouldBeNull();
        Canned.ToolResultText(response).ShouldBe("already ticked — no change");
    }

    [Fact]
    public async Task PlanningMutationTools_ProjectTheUniformEchoKeySet()
    {
        var upsert = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "# Uniform echo task"
        })))!.AsObject();
        upsert.Select(p => p.Key).ToArray().ShouldBe(
            ["id", "kind", "status", "version", "order", "frozenAt", "updatedAt", "bodyChars", "lastLogLine"]);
        upsert["lastLogLine"]!.GetValue<string>().ShouldBe("stubbed creation log line");

        var patch = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "goal",
            ["content"] = "<goal>\nRewired.\n</goal>"
        })))!.AsObject();
        patch.Select(p => p.Key).ToArray().ShouldBe(
            ["id", "version", "status", "lastLogLine", "section", "bodyChars"]);

        var appendLog = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync("planning_append_log", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["note"] = "uniform echo probe"
        })))!.AsObject();
        appendLog.Select(p => p.Key).ToArray().ShouldBe(["id", "version", "status", "lastLogLine"]);

        var setStatus = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "review",
            ["logNote"] = "uniform echo probe"
        })))!.AsObject();
        setStatus.Select(p => p.Key).ToArray().ShouldBe(["id", "version", "status", "lastLogLine"]);

        var validate = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync("planning_validate", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString()
        })))!.AsObject();
        validate.Select(p => p.Key).ToArray().ShouldBe(["id", "version", "status", "lastLogLine"]);

        var setFeatureStatus = JsonNode.Parse(Canned.ToolResultText(await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["status"] = "done",
            ["logNote"] = "by user request in-session"
        })))!.AsObject();
        setFeatureStatus.Select(p => p.Key).ToArray().ShouldBe(["id", "version", "status", "lastLogLine"]);
        setFeatureStatus["id"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
    }

    [Fact]
    public async Task PlanningUpsertArtifact_Demotion_EchoCarriesStatusChanged()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts",
            200,
            Canned.PlanningArtifactDemotedJson);

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "spec",
            ["body"] = "# Spec\n\n<goal>\nReworked after review.\n</goal>",
            ["logNote"] = "reopened for rework"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation["status"]!.GetValue<string>().ShouldBe("draft");
        confirmation["statusChanged"]!.GetValue<string>().ShouldBe("validated → draft");
        confirmation["featureStatusChanged"]!.GetValue<string>().ShouldBe("review → inProgress");
    }

    [Fact]
    public async Task PlanningPatchArtifact_VoidedProof_EchoesValidationProofVoided()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            200,
            """{"artifactId":"99999999-9999-9999-9999-999999999999","kind":"spec","section":"goal","version":4,"bodyChars":88,"status":"draft","lastLogLine":"reopened for rework","statusChanged":"validated → draft","validationProofVoided":"2026-10-22T10:14:06Z"}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "goal",
            ["content"] = "<goal>\nReworked after review.\n</goal>",
            ["logNote"] = "reopened for rework"
        });

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine", "section", "bodyChars", "statusChanged", "validationProofVoided"]);
        echo["validationProofVoided"]!.GetValue<string>().ShouldBe("2026-10-22T10:14:06Z");
    }

    [Fact]
    public async Task PlanningUpsertArtifact_VoidedProof_EchoCarriesValidationProofVoided()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts",
            200,
            """{"id":"66666666-6666-6666-6666-666666666666","kind":"spec","status":"draft","review":"light","order":0,"body":"# Demo spec\n\n<goal>\nReworked after review.\n</goal>","frozenAt":null,"version":3,"createdAt":"2026-09-08T10:05:00Z","updatedAt":"2026-10-22T10:20:00Z","log":[{"date":"2026-10-22","text":"artifact demoted: validated → draft"}],"statusChanged":"validated → draft","validationProofVoided":"2026-10-22T10:14:06Z"}""");

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "spec",
            ["body"] = "# Spec\n\n<goal>\nReworked after review.\n</goal>",
            ["logNote"] = "reopened for rework"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.Select(p => p.Key).ToArray()
            .ShouldBe(["id", "kind", "status", "version", "order", "frozenAt", "updatedAt", "bodyChars", "lastLogLine", "statusChanged", "validationProofVoided"]);
        confirmation["validationProofVoided"]!.GetValue<string>().ShouldBe("2026-10-22T10:14:06Z");
    }

    [Fact]
    public async Task PlanningPatchArtifact_UntickWithSectionOrAlone_RejectedWithoutHttpCall()
    {
        var withSection = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "goal",
            ["content"] = "<goal>\nReworked.\n</goal>",
            ["untick"] = true
        });

        ToolErrorText(withSection).ShouldContain("never several and never none");

        var alone = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["untick"] = true
        });

        ToolErrorText(alone).ShouldContain("never several and never none");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(" ", "x", null, null, null, "non-blank section")]
    [InlineData("goal", " ", null, null, null, "replacement content")]
    [InlineData(null, null, 1, "first", null, "not both")]
    [InlineData(null, null, null, "", null, "non-blank acText")]
    [InlineData(null, null, null, null, null, "never several and never none")]
    [InlineData("goal", "content", 1, null, null, "never several and never none")]
    [InlineData("goal", "content", null, null, "stray brief", "never several and never none")]
    [InlineData(null, null, null, null, " ", "non-blank single-line")]
    [InlineData(null, null, null, null, "first\nsecond", "non-blank single-line")]
    public async Task PlanningPatchArtifact_MalformedShape_RejectedWithoutHttpCall(
        string? section, string? content, int? acIndex, string? acText, string? brief, string expectedFragment)
    {
        var arguments = new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        };
        if (section is not null)
        {
            arguments["section"] = section;
        }
        if (content is not null)
        {
            arguments["content"] = content;
        }
        if (acIndex is not null)
        {
            arguments["acIndex"] = acIndex;
        }
        if (acText is not null)
        {
            arguments["acText"] = acText;
        }
        if (brief is not null)
        {
            arguments["brief"] = brief;
        }

        var response = await Driver.CallToolAsync("planning_patch_artifact", arguments);

        var text = ToolErrorText(response);
        text.ShouldContain(expectedFragment);
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningPatchArtifact_BriefOverCap_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["brief"] = new string('x', 501)
        });

        ToolErrorText(response).ShouldContain("at most 500 chars");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningValidate_Description_DocumentsProofAndThe409()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var description = tools["planning_validate"]!["description"]!.GetValue<string>();

        description.ShouldContain("overmind_notify(kind=approval");
        description.ShouldContain("notify → reply → validate");
        description.ShouldContain("ValidationProofRequired");
        description.ShouldContain("human door");
        description.ShouldContain("every edit voids it");
    }

    [Fact]
    public async Task PlanningValidate_ProofRequired409_SurfacesTheActionableDetail()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/validate",
            409,
            Canned.PlanningValidationProofRequiredJson);

        var response = await Driver.CallToolAsync("planning_validate", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("overmind API returned 409");
        text.ShouldContain("Human approval required: send overmind_notify(kind=approval, featureSlug=demo-feature)");
        text.ShouldContain("or validate via the UI");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningPatchArtifact_LogSection_SurfacesTheAppendLogRedirect()
    {
        Stub.Route(
            "POST",
            $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/patch",
            400,
            """{"title":"PatchLogSectionNotReplaceable","detail":"The log section is append-only; replace_section cannot rewrite it. Append a dated line via planning_append_log or correct the newest own line via planning_edit_log; if history genuinely must be rewritten, resend the full body via planning_upsert_artifact.","status":400}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "log",
            ["content"] = "- 2026-09-22 — forged history"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("append-only");
        text.ShouldContain("planning_append_log");
        text.ShouldContain("planning_upsert_artifact");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningValidate_Ungated_ConfirmsValidation()
    {
        var response = await Driver.CallToolAsync("planning_validate", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString()
        });

        FindRequest("POST", $"/api/agent/planning/artifacts/{Canned.PlanningSpecArtifactId}/validate");
        response["result"]!["isError"].ShouldBeNull();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.AsObject().Select(p => p.Key).ToArray()
            .ShouldBe(["id", "version", "status", "lastLogLine"]);
        echo["id"]!.GetValue<string>().ShouldBe(Canned.PlanningSpecArtifactId.ToString());
        echo["status"]!.GetValue<string>().ShouldBe("validated");
        echo["lastLogLine"]!.GetValue<string>().ShouldContain("validated with human proof");
    }

    [Fact]
    public async Task PlanningValidate_GatedByHumanGate_StopErrorLeadsAndNothingElseIsCalled()
    {
        Stub.Route(
            "POST",
            $"/api/agent/planning/artifacts/{Canned.PlanningSpecArtifactId}/validate",
            409,
            Canned.PlanningValidateGateJson);

        var response = await Driver.CallToolAsync("planning_validate", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldStartWith("STOP:");
        text.ShouldContain("awaits human validation");
        text.ShouldContain("approve it in the Overmind UI");

        Stub.Requests.Count(r => r.Path.StartsWith("/api/agent/", StringComparison.Ordinal)
                && !r.Path.StartsWith("/api/agent/sessions", StringComparison.Ordinal))
            .ShouldBe(1);
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningValidate_AsMigratedForwardsFlagAndNote()
    {
        var response = await Driver.CallToolAsync("planning_validate", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString(),
            ["asMigrated"] = true,
            ["logNote"] = "frozen v9 on disk, user-approved"
        });

        var request = FindRequest("POST", $"/api/agent/planning/artifacts/{Canned.PlanningSpecArtifactId}/validate");
        var submitted = JsonNode.Parse(request.Body)!;
        submitted["asMigrated"]!.GetValue<bool>().ShouldBeTrue();
        submitted["logNote"]!.GetValue<string>().ShouldBe("frozen v9 on disk, user-approved");

        response["result"]!["isError"].ShouldBeNull();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["status"]!.GetValue<string>().ShouldBe("validated");
        echo["lastLogLine"]!.GetValue<string>()
            .ShouldBe("validated as migrated — pre-approved on disk: frozen v9 on disk, user-approved");
    }

    [Fact]
    public async Task PlanningValidate_AsMigratedWithoutLogNote_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_validate", new JsonObject
        {
            ["artifactId"] = Canned.PlanningSpecArtifactId.ToString(),
            ["asMigrated"] = true
        });

        var text = ToolErrorText(response);
        text.ShouldContain("logNote is required");
        text.ShouldContain("asMigrated");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/validate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningMaterialize_PostsToTheMaterializeRouteAndEchoesWrittenAndSkippedCounts()
    {
        var response = await Driver.CallToolAsync("planning_materialize", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var request = FindRequest("POST", $"/api/projects/{Canned.ProjectId}/planning/materialize");
        request.Path.ShouldBe($"/api/projects/{Canned.ProjectId}/planning/materialize");
        JsonNode.Parse(request.Body)!["force"]!.GetValue<bool>().ShouldBeFalse();

        var text = Canned.ToolResultText(response);
        var echo = JsonNode.Parse(text)!.AsObject();
        echo["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
        echo["strategy"]!.GetValue<string>().ShouldBe("on");
        echo["written"]!.GetValue<int>().ShouldBe(3);
        echo["skipped"]!.GetValue<int>().ShouldBe(2);
        echo["force"]!.GetValue<bool>().ShouldBeFalse();
        echo["lastSuccess"]!.GetValue<string>().ShouldBe("2026-09-23T10:15:00Z");
        echo["lastAttempt"]!.GetValue<string>().ShouldBe("2026-09-24T09:30:00Z");
        text.ShouldNotContain("sha256");
        text.ShouldNotContain("Overwrote untracked file");
    }

    [Fact]
    public async Task PlanningMaterialize_WithForce_PostsItAndCarriesItInTheEcho()
    {
        var response = await Driver.CallToolAsync("planning_materialize", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["force"] = true
        });

        var request = FindRequest("POST", $"/api/projects/{Canned.ProjectId}/planning/materialize");
        JsonNode.Parse(request.Body)!["force"]!.GetValue<bool>().ShouldBeTrue();

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["force"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task PlanningMaterialize_SkipsPresent_EchoCarriesPathAndReasonLines()
    {
        var response = await Driver.CallToolAsync("planning_materialize", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        var head = echo["skippedHead"]!.AsArray();
        head.Count.ShouldBe(2);
        head[0]!.GetValue<string>().ShouldBe(
            "BACKLOG/foreign/feature.md: content already matches the board");
        head[1]!.GetValue<string>().ShouldBe(
            "BACKLOG/demo-feature/tasks/01-build.md: content already matches the board");
    }

    [Fact]
    public async Task PlanningMaterialize_CleanPass_OmitsTheSkippedHead()
    {
        Stub.Route(
            "POST",
            $"/api/projects/{Canned.ProjectId}/planning/materialize",
            200,
            Canned.PlanningMaterializeCleanReportJson);

        var response = await Driver.CallToolAsync("planning_materialize", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["written"]!.GetValue<int>().ShouldBe(2);
        echo["skipped"]!.GetValue<int>().ShouldBe(0);
        echo["lastSuccess"]!.GetValue<string>().ShouldBe("2026-09-24T09:30:00Z");
        echo.ContainsKey("skippedHead").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningMaterialize_AllSkipped_SurfacesTheConflictDetailAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/projects/{Canned.ProjectId}/planning/materialize",
            409,
            Canned.PlanningMaterializeAllSkippedJson);

        var response = await Driver.CallToolAsync("planning_materialize", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Every planned file was skipped; nothing was written");
        text.ShouldContain("BACKLOG/foreign/feature.md");
        text.ShouldContain("re-run materialize with force");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningImportFeature_PostsThePathToTheImportRouteAndEchoesTheReport()
    {
        var folder = "/repo/BACKLOG/058-demo";

        var response = await Driver.CallToolAsync("planning_import_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["path"] = folder
        });

        var request = FindRequest("POST", $"/api/agent/projects/{Canned.ProjectId}/planning/import");
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/import");
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body.Count.ShouldBe(1);
        body["path"]!.GetValue<string>().ShouldBe(folder);

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["slug"]!.GetValue<string>().ShouldBe("058-demo");
        echo["status"]!.GetValue<string>().ShouldBe("inProgress");
        echo["created"]!.GetValue<bool>().ShouldBeTrue();
        echo["artifactsCreated"]!.GetValue<int>().ShouldBe(2);
        echo["artifactsUpdated"]!.GetValue<int>().ShouldBe(0);
        echo["trackedFiles"]!.GetValue<int>().ShouldBe(3);
        echo["materialization"]!.GetValue<string>().ShouldBe("on");
        echo["notes"]!.AsArray().Count.ShouldBe(1);
    }

    [Fact]
    public async Task PlanningImportFeature_PathOutsideRoot_SurfacesTheRejectedDetailAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/import",
            400,
            Canned.PlanningImportOutsideRootJson);

        var response = await Driver.CallToolAsync("planning_import_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["path"] = "/elsewhere/BACKLOG/058-demo"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Importing '/elsewhere/BACKLOG/058-demo' failed");
        text.ShouldContain("resolves outside the project directory '/repo'");
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlanningImportFeature_BlankPath_RejectedWithoutHttpCall(string? path)
    {
        var response = await Driver.CallToolAsync("planning_import_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["path"] = path
        });

        var text = ToolErrorText(response);
        text.ShouldContain("path is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/planning/import", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningExportBacklog_PostsTheTargetPathToTheAgentExportRouteAndEchoesTheCompactSnapshot()
    {
        var target = "/repo/docs/planning-snapshot";

        var response = await Driver.CallToolAsync("planning_export_backlog", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["targetPath"] = target
        });

        var request = FindRequest("POST", $"/api/agent/projects/{Canned.ProjectId}/planning/export");
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/export");
        var body = JsonNode.Parse(request.Body)!.AsObject();
        body["targetPath"]!.GetValue<string>().ShouldBe(target);

        var text = Canned.ToolResultText(response);
        var echo = JsonNode.Parse(text)!.AsObject();
        echo["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
        echo["target"]!.GetValue<string>().ShouldBe("/repo/docs/planning-snapshot");
        echo["files"]!.GetValue<int>().ShouldBe(3);
        text.ShouldNotContain("body");
    }

    [Fact]
    public async Task PlanningExportBacklog_TargetRefused_SurfacesTheRefusalAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/export",
            409,
            Canned.PlanningExportTargetNotEmptyJson);

        var response = await Driver.CallToolAsync("planning_export_backlog", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["targetPath"] = "/repo/docs/planning-snapshot"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Export target '/repo/docs/planning-snapshot' is not empty");
        text.ShouldContain("refuses an existing tree");
        Driver.HasExited.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlanningExportBacklog_BlankTargetPath_RejectedWithoutHttpCall(string? targetPath)
    {
        var response = await Driver.CallToolAsync("planning_export_backlog", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["targetPath"] = targetPath
        });

        var text = ToolErrorText(response);
        text.ShouldContain("targetPath is required");
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/planning/export", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningExportBacklog_Description_PinsTheSnapshotContract()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_export_backlog"]!["description"]!.GetValue<string>().ShouldBe(
            "Renders the full materialized mirror (planning tree + entries projection) into an explicit " +
            "target directory as plain greppable files — the \"commit the planning alongside the code\" path. " +
            "Uses the same document renderers as the materializer pass, so the snapshot is byte-identical to " +
            "what a materialize run would write; zero tracking rows and zero drift machinery are created. " +
            "The target must be an empty directory — non-empty refuses with 409 (a snapshot, not a sync) — " +
            "and it must not sit inside BACKLOG/ or ENTRIES/. targetPath is always explicit; there is no default. " +
            "Compact echo: written file count + target, no body echo.");
    }

    [Fact]
    public async Task PlanningMaterialize_Description_PinsTheRerunContract()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_materialize"]!["description"]!.GetValue<string>().ShouldBe(
            "Triggers a materialization pass: the full plan is computed first, then written atomically — " +
            "any guarded file (untracked at a planned path, or tracked with local edits not on the board) " +
            "aborts the whole run with a 409 listing every refusal, and nothing is written that run. " +
            "Skips in the report are byte-identical no-ops (content already matches the board), not guarded files. " +
            "A normal board mutation already enqueues a pass that runs shortly after in the background; " +
            "this tool runs the pass inline and waits for the report — call it after an import, " +
            "a strategy switch, or when onboard reports a failed attempt or stale mirror state. " +
            "force adopts guarded files with per-file warnings instead of refusing; transcribe hand edits to the board before forcing.");
    }

    [Fact]
    public async Task PlanningMaterializeStatus_CallsTheAgentStatusRouteAndEchoesThePayload()
    {
        var response = await Driver.CallToolAsync("planning_materialize_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var request = FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/materialization-status");
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/materialization-status");

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo["projectId"]!.GetValue<string>().ShouldBe(Canned.ProjectId.ToString());
        echo["lastSuccess"]!.GetValue<string>().ShouldBe("2026-09-23T10:15:00Z");
        echo["lastAttempt"]!.GetValue<string>().ShouldBe("2026-09-24T09:30:00Z");
        echo["lastAttemptReason"].ShouldBeNull();
        echo["trackedFiles"]!.GetValue<int>().ShouldBe(2);
        echo["stalePaths"]!.AsArray().Count.ShouldBe(1);
        echo["strategy"]!.GetValue<string>().ShouldBe("on");
        echo["targetDir"]!.GetValue<string>().ShouldBe("/repo/BACKLOG");

        var run = echo["lastRun"]!.AsObject();
        run["startedAt"]!.GetValue<string>().ShouldBe("2026-09-24T09:30:00Z");
        run["finishedAt"]!.GetValue<string>().ShouldBe("2026-09-24T09:30:05Z");
        run["atomic"]!.GetValue<bool>().ShouldBeTrue();
        run["written"]!.AsArray().Count.ShouldBe(2);
        run["skipped"]!.AsArray().ShouldHaveSingleItem();
        run["refused"]!.AsArray().Count.ShouldBe(0);
        run["skipped"]!.AsArray()[0]!["relativePath"]!.GetValue<string>().ShouldBe("BACKLOG/foreign/feature.md");
    }

    [Fact]
    public async Task PlanningMaterializeStatus_AbsentReport_RendersNoReportForTheAttempt()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/materialization-status",
            Canned.PlanningMaterializeStatusNoReportJson);

        var response = await Driver.CallToolAsync("planning_materialize_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        var echo = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        echo.ContainsKey("lastRun").ShouldBeTrue();
        echo["lastRun"].ShouldBeNull();
        echo["lastAttemptReason"]!.GetValue<string>().ShouldContain("Duplicate materialization target");
        echo["stalePaths"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task PlanningMaterializeStatus_Description_PinsTheDiagnosticContract()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_materialize_status"]!["description"]!.GetValue<string>().ShouldBe(
            "Mirror diagnostics without filesystem access: returns the project's materialization state as one " +
            "structured payload — lastSuccess vs lastAttempt (lastAttemptReason carries the failure when the " +
            "attempt did not succeed), the persisted last-run report per file (startedAt, finishedAt, atomic, " +
            "written paths, skipped and refused with reasons), trackedFiles, stalePaths (tracked files the last " +
            "run neither wrote nor skipped, plus tracked files whose disk bytes drifted), strategy and targetDir. " +
            "lastRun is null when the failed attempt persisted no report — read it as no report for this attempt. " +
            "Read-only: never triggers a pass; use planning_materialize for that.");
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("")]
    public async Task PlanningUpsertArtifact_UnknownKind_RejectedWithoutHttpCall(string kind)
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = kind,
            ["body"] = "body"
        });

        var text = ToolErrorText(response);
        text.ShouldContain($"Invalid artifact kind '{kind}'");
        text.ShouldContain("spec, plan, task, result");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningUpsertArtifact_UnknownReview_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "body",
            ["review"] = "medium"
        });

        ToolErrorText(response).ShouldContain("Invalid review level 'medium'");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningSetStatus_UnknownStatus_RejectedWithoutHttpCall()
    {
        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "finished",
            ["logNote"] = "done with it"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Invalid artifact status 'finished'");
        text.ShouldContain("todo, inProgress, review, done, blocked");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningSetStatus_EmptyLogNote_RejectedClientSide()
    {
        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "done",
            ["logNote"] = "   "
        });

        var text = ToolErrorText(response);
        text.ShouldContain("logNote is required");
        text.ShouldContain("artifact");
        Stub.Requests.ShouldNotContain(r => r.Method == "POST" && r.Path.EndsWith("/status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningPatchArtifact_InlineSectionHint_SurfacesThroughTheDoor()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            400,
            """{"title":"PatchSectionNotFound","detail":"Section 'anti-scope' was not found. Sections present: goal, scope. A one-line '<anti-scope>…</anti-scope>' section exists in the body — one-line sections need the canonical multi-line form; re-send the body once (any upsert canonicalizes it).","status":400}""");

        var response = await Driver.CallToolAsync("planning_patch_artifact", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["section"] = "anti-scope",
            ["content"] = "<anti-scope>\nNo parser changes.\n</anti-scope>"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("one-line");
        text.ShouldContain("re-send the body once");
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
