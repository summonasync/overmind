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
    public async Task ToolsList_ContainsTheTwentyTwoPlanningTools()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        string[] expected =
        [
            "planning_list_features",
            "planning_read_feature",
            "planning_read_artifact",
            "planning_current_task",
            "planning_create_feature",
            "planning_upsert_artifact",
            "planning_upsert_tasks",
            "planning_set_status",
            "planning_append_log",
            "planning_patch_artifact",
            "planning_validate",
            "planning_set_current",
            "planning_set_feature_status",
            "planning_task_queue",
            "planning_move_feature",
            "planning_set_feature_priority",
            "planning_list_feature_dependencies",
            "planning_add_feature_dependency",
            "planning_remove_feature_dependency",
            "planning_materialize",
            "planning_materialize_status",
            "planning_import_feature"
        ];
        foreach (var name in expected)
        {
            tools.ShouldContainKey(name);
            tools[name]!["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        }

        tools["planning_list_features"]!["description"]!.GetValue<string>().ShouldContain("board");
        tools["planning_current_task"]!["description"]!.GetValue<string>().ShouldContain("STOP");
        tools["planning_current_task"]!["description"]!.GetValue<string>().ShouldContain("unclosed blockers");
        tools["planning_set_status"]!["description"]!.GetValue<string>().ShouldContain("logNote");
        tools["planning_upsert_artifact"]!["description"]!.GetValue<string>().ShouldContain("brief");
        tools["planning_create_feature"]!["description"]!.GetValue<string>().ShouldContain("priority");
        tools["planning_validate"]!["description"]!.GetValue<string>().ShouldContain("human validation");
        tools["planning_set_current"]!["description"]!.GetValue<string>().ShouldContain("unpins");
        tools["planning_set_current"]!["description"]!.GetValue<string>().ShouldContain("unclosed blockers");
        tools["planning_set_feature_status"]!["description"]!.GetValue<string>().ShouldContain("user's explicit request");
        tools["planning_set_feature_status"]!["description"]!.GetValue<string>().ShouldContain("human door");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("priority asc");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("dragging");
        tools["planning_task_queue"]!["description"]!.GetValue<string>().ShouldContain("readiness 0-3");
        tools["planning_move_feature"]!["description"]!.GetValue<string>().ShouldContain("band-scoped");
        tools["planning_move_feature"]!["description"]!.GetValue<string>().ShouldContain("toPosition");
        tools["planning_move_feature"]!["description"]!.GetValue<string>().ShouldContain("planning_set_feature_priority");
        tools["planning_set_feature_priority"]!["description"]!.GetValue<string>().ShouldContain("explicit approval");
        tools["planning_set_feature_priority"]!["description"]!.GetValue<string>().ShouldContain("tail");
        tools["planning_set_feature_priority"]!["description"]!.GetValue<string>().ShouldContain("planning_move_feature");
        tools["planning_list_feature_dependencies"]!["description"]!.GetValue<string>().ShouldContain("done or review");
        tools["planning_add_feature_dependency"]!["description"]!.GetValue<string>().ShouldContain("cycle");
        tools["planning_remove_feature_dependency"]!["description"]!.GetValue<string>().ShouldContain("no-op");
        tools["planning_patch_artifact"]!["description"]!.GetValue<string>().ShouldContain("acceptance");
        tools["planning_materialize"]!["description"]!.GetValue<string>().ShouldContain("board mutation");
        tools["planning_materialize_status"]!["description"]!.GetValue<string>().ShouldContain("stalePaths");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("all-or-nothing");
        tools["planning_upsert_tasks"]!["description"]!.GetValue<string>().ShouldContain("one feature auto-transition evaluation");
        tools["planning_import_feature"]!["description"]!.GetValue<string>().ShouldContain("never pass through the model");
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
        text.ShouldNotContain("missingSections");
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
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningReadArtifact_Description_PinsTheReadCostTiers()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_read_artifact"]!["description"]!.GetValue<string>().ShouldBe(
            "Reads one artifact by id — without sections, one artifact, full body; with sections, the cheapest read: only the named sections. missingSections names requested-but-absent sections; a read never fails on a missing one.");
    }

    [Fact]
    public async Task PlanningReadFeature_Description_PinsTheMostExpensiveReadTier()
    {
        var response = await Driver.ListToolsAsync();

        var tools = response["result"]!["tools"]!.AsArray()
            .ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);

        tools["planning_read_feature"]!["description"]!.GetValue<string>().ShouldBe(
            "Reads one feature in full: its body plus every spec, plan, task and result artifact with statuses and log lines — the most expensive read: the whole feature with all artifacts; prefer planning_read_artifact for one document.");
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
    public async Task PlanningUpsertTasks_ApiSurfacesPerItemIndexInDetail()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts/tasks",
            400,
            """{"title":"ChangeNoteRequired","detail":"tasks[1]: A logNote is required to modify a frozen artifact.","status":400}""");

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
        confirmation["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        confirmation["status"]!.GetValue<string>().ShouldBe("review");
        confirmation["lastLogLine"]!.GetValue<string>().ShouldBe("implementation finished, handing to review");
        confirmation.ContainsKey("warnings").ShouldBeFalse();
    }

    [Fact]
    public async Task PlanningSetStatus_ApiReportsWarnings_SurfacesThemInTheConfirmation()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/status",
            200,
            """{"warnings":["8 of 12 acceptance items unticked"]}""");

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "done",
            ["logNote"] = "field report closed the loop"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation["status"]!.GetValue<string>().ShouldBe("done");
        var warnings = confirmation["warnings"]!.AsArray();
        warnings.Count.ShouldBe(1);
        warnings[0]!.GetValue<string>().ShouldBe("8 of 12 acceptance items unticked");
    }

    [Fact]
    public async Task PlanningSetStatus_ApiReportsNoWarnings_OmitsTheProperty()
    {
        Stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/status", 200, "{}");

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["status"] = "done",
            ["logNote"] = "every item ticked"
        });

        var confirmation = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        confirmation.ContainsKey("warnings").ShouldBeFalse();
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
    public async Task PlanningSetCurrent_BlockedFeature_RefusedWithStopDiagnosticsNamingBlockers()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/current",
            409,
            Canned.PlanningSetCurrentBlockedJson);

        var response = await Driver.CallToolAsync("planning_set_current", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["isCurrent"] = true
        });

        var text = ToolErrorText(response);
        text.ShouldStartWith("STOP:");
        text.ShouldContain("is blocked by unclosed features");
        text.ShouldContain("gate-locked (ready)");
        text.ShouldContain("Pinning is refused");
        text.ShouldNotContain("overmind API returned");
        Driver.HasExited.ShouldBeFalse();
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
    public async Task PlanningSetFeaturePriority_PutsPriorityThroughTheHumanDoor()
    {
        var response = await Driver.CallToolAsync("planning_set_feature_priority", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["priority"] = "P0"
        });

        var request = Stub.Requests.Single(r => r.Method == "PUT");
        request.Path.ShouldBe(
            $"/api/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");
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
        Stub.Requests.ShouldNotContain(r => r.Method == "PUT");
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

        response["result"]!["isError"].ShouldBeNull();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("task artifact");
        text.ShouldContain(Canned.PlanningTaskTwoId.ToString());
        text.ShouldContain("section 'goal'");
        text.ShouldContain("version 3");
        text.ShouldContain("412 body chars");
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

        Canned.ToolResultText(response).ShouldContain("section 'acceptance-criteria'");
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
        text.Length.ShouldBeLessThan(200);
        text.ShouldContain(Canned.PlanningTaskTwoId.ToString());
        text.ShouldContain("version 3");
        text.ShouldContain("412 body chars");
    }

    [Theory]
    [InlineData(" ", "x", null, null, "non-blank section")]
    [InlineData("goal", " ", null, null, "replacement content")]
    [InlineData("goal", null, null, null, "replacement content")]
    [InlineData(null, null, 1, "first", "not both")]
    [InlineData(null, null, null, "", "non-blank acText")]
    [InlineData(null, null, null, null, "not both and not neither")]
    [InlineData("goal", "content", 1, null, "not both and not neither")]
    public async Task PlanningPatchArtifact_MalformedShape_RejectedWithoutHttpCall(
        string? section, string? content, int? acIndex, string? acText, string expectedFragment)
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

        var response = await Driver.CallToolAsync("planning_patch_artifact", arguments);

        var text = ToolErrorText(response);
        text.ShouldContain(expectedFragment);
        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/patch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningPatchArtifact_LogSection_SurfacesTheAppendLogRedirect()
    {
        Stub.Route(
            "POST",
            $"/api/agent/planning/artifacts/{Canned.PlanningTaskTwoId}/patch",
            400,
            """{"title":"PatchLogSectionNotReplaceable","detail":"The log section is append-only; replace_section cannot rewrite it. Append a dated line via planning_append_log or the artifact log endpoint; if history genuinely must be rewritten, resend the full body via planning_upsert_artifact.","status":400}""");

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
        Canned.ToolResultText(response).ShouldContain("validated as migrated");
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
            "A normal board mutation already triggers a pass automatically; call this after an import, " +
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

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
