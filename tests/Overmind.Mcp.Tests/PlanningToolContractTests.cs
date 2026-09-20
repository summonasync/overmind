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
    public async Task ToolsList_ContainsTheTenPlanningTools()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        string[] expected =
        [
            "planning_list_features",
            "planning_read_feature",
            "planning_current_task",
            "planning_create_feature",
            "planning_upsert_artifact",
            "planning_set_status",
            "planning_append_log",
            "planning_validate",
            "planning_set_current",
            "planning_set_feature_status"
        ];
        foreach (var name in expected)
        {
            tools.ShouldContainKey(name);
            tools[name]!["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        }

        tools["planning_list_features"]!["description"]!.GetValue<string>().ShouldContain("board");
        tools["planning_current_task"]!["description"]!.GetValue<string>().ShouldContain("STOP");
        tools["planning_set_status"]!["description"]!.GetValue<string>().ShouldContain("logNote");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("brief");
        tools["planning_create_feature"]!["description"]!.GetValue<string>().ShouldContain("priority");
        tools["planning_validate"]!["description"]!.GetValue<string>().ShouldContain("human validation");
        tools["planning_set_current"]!["description"]!.GetValue<string>().ShouldContain("unpins");
        tools["planning_set_feature_status"]!["description"]!.GetValue<string>().ShouldContain("user's explicit request");
        tools["planning_set_feature_status"]!["description"]!.GetValue<string>().ShouldContain("human door");
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
        Canned.ToolResultText(response).ShouldContain("\"isCurrent\":true");
        Canned.ToolResultText(response).ShouldContain("\"priority\":\"p1\"");
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

        Canned.ToolResultText(response).ShouldContain("Demo spec");
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

        Canned.ToolResultText(response).ShouldContain("Wire the API");
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

        Canned.ToolResultText(response).ShouldContain("demo-feature");
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
        body.AsObject().Select(p => p.Key).ToArray().ShouldBe(
            ["kind", "body", "order", "review", "logNote", "brief"], ignoreOrder: true);

        response["result"]!["isError"].ShouldBeNull();
    }

    [Fact]
    public async Task PlanningUpsertArtifact_WithoutBrief_OmitsItFromThePostedJson()
    {
        await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "spec",
            ["body"] = "# Spec"
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/artifacts", StringComparison.Ordinal));
        JsonNode.Parse(request.Body)!.AsObject().ContainsKey("brief").ShouldBeFalse();
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

        Canned.ToolResultText(response).ShouldContain("review");
    }

    [Fact]
    public async Task PlanningSetCurrent_PostsIsCurrentToAgentRoute()
    {
        var response = await Driver.CallToolAsync("planning_set_current", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["isCurrent"] = true
        });

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/current", StringComparison.Ordinal));
        request.Path.ShouldBe(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/current");
        JsonNode.Parse(request.Body)!["isCurrent"]!.GetValue<bool>().ShouldBeTrue();

        Canned.ToolResultText(response).ShouldContain("pinned as current");
    }

    [Fact]
    public async Task PlanningSetCurrent_UnknownFeature_ApiErrorSurfacesAsToolError()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/current",
            404,
            Canned.PlanningFeatureNotFoundJson);

        var response = await Driver.CallToolAsync("planning_set_current", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["isCurrent"] = true
        });

        var text = ToolErrorText(response);
        text.ShouldContain("Feature 'demo-feature' was not found");
        Driver.HasExited.ShouldBeFalse();
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

        Canned.ToolResultText(response).ShouldContain("moved to 'done'");
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
    public async Task PlanningAppendLog_PostsTheNote()
    {
        var response = await Driver.CallToolAsync("planning_append_log", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["note"] = "halfway through the wiring"
        });

        var request = Stub.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/log", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/log");
        JsonNode.Parse(request.Body)!["note"]!.GetValue<string>().ShouldBe("halfway through the wiring");

        Canned.ToolResultText(response).ShouldContain("appended");
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
        Canned.ToolResultText(response).ShouldContain("validated");
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

        Stub.Requests.Count(r => r.Path.StartsWith("/api/agent/", StringComparison.Ordinal)).ShouldBe(1);
        Driver.HasExited.ShouldBeFalse();
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

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
