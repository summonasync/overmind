using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class PlanningFieldProjectionContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task ListFeatures_FieldsSlice_ReturnsIdPlusRequestedKeysInRequestOrder()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = "phase,briefExcerpt"
        });

        response["result"]!["isError"].ShouldBeNull();

        var rows = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        rows.Count.ShouldBe(2);
        foreach (var row in rows)
        {
            row!.AsObject().Select(pair => pair.Key).ShouldBe(["slug", "phase", "briefExcerpt"]);
        }

        var demo = rows.Single(r => r!["slug"]!.GetValue<string>() == "demo-feature")!;
        demo["phase"]!.GetValue<string>().ShouldBe("in progress");
        demo["briefExcerpt"]!.GetValue<string?>().ShouldBe("Crash on save when the workspace file is read-only.");
    }

    [Fact]
    public async Task ListFeatures_FieldsRequestingSlug_KeepsSlugOnceInTheLead()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = "slug,status"
        });

        var rows = JsonNode.Parse(Canned.ToolResultText(response))!.AsArray();
        var demo = rows.Single(r => r!["slug"]!.GetValue<string>() == "demo-feature")!;
        demo["status"]!.GetValue<string>().ShouldBe("inProgress");
        demo.AsObject().Select(pair => pair.Key).ShouldBe(["slug", "status"]);
    }

    [Fact]
    public async Task TaskQueue_FieldsSlice_ProjectsItemsAndKeepsTheEnvelope()
    {
        var response = await Driver.CallToolAsync("planning_task_queue", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = "status,taskTotal"
        });

        response["result"]!["isError"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        var rows = JsonNode.Parse(text)!["items"]!.AsArray();
        rows.Count.ShouldBe(2);
        foreach (var row in rows)
        {
            row!.AsObject().Select(pair => pair.Key).ShouldBe(["slug", "status", "taskTotal"]);
        }

        text.ShouldNotContain("\"title\"");
        text.ShouldNotContain("\"briefExcerpt\"");
    }

    [Fact]
    public async Task TaskQueue_Compact_SwapsRowsForTheUsualShape()
    {
        var response = await Driver.CallToolAsync("planning_task_queue", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["compact"] = true
        });

        response["result"]!["isError"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        text.ShouldNotContain("briefExcerpt");
        text.ShouldNotContain("taskCounts");
        text.ShouldNotContain("createdAt");

        var rows = JsonNode.Parse(text)!["items"]!.AsArray();
        var demo = rows.Single(r => r!["slug"]!.GetValue<string>() == "demo-feature")!.AsObject();
        demo.Select(pair => pair.Key).ShouldBe(
            ["slug", "title", "priority", "status", "queuePosition", "phase", "readiness", "taskDone", "taskTotal",
                "claimedByAlias", "claimedByVendor", "claimedAt", "claimState"]);
        demo["queuePosition"]!.GetValue<int>().ShouldBe(2);
        demo["phase"]!.GetValue<string>().ShouldBe("in progress");
    }

    [Theory]
    [InlineData("planning_list_features")]
    [InlineData("planning_task_queue")]
    [InlineData("planning_read_feature")]
    public async Task UnknownField_RejectedNamingTheAllowedSet_BeforeAnyHttp(string tool)
    {
        var args = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = "slug,banana"
        };
        if (tool == "planning_read_feature")
        {
            args["slug"] = Canned.PlanningSlug;
        }

        var response = await Driver.CallToolAsync(tool, args);

        var text = ToolErrorText(response);
        text.ShouldContain("banana");
        text.ShouldContain("Allowed fields:");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("planning_list_features")]
    [InlineData("planning_task_queue")]
    [InlineData("planning_read_feature")]
    public async Task FieldsWithCompact_RejectedNamingBothKnobs_BeforeAnyHttp(string tool)
    {
        var args = new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = "status",
            ["compact"] = true
        };
        if (tool == "planning_read_feature")
        {
            args["slug"] = Canned.PlanningSlug;
        }

        var response = await Driver.CallToolAsync(tool, args);

        var text = ToolErrorText(response);
        text.ShouldContain("fields and compact are mutually exclusive");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyFieldsList_RejectedAsACallerBug_BeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["fields"] = ",,,"
        });

        ToolErrorText(response).ShouldContain("projection of nothing");

        Stub.Requests.ShouldNotContain(r => r.Path.Contains("/planning/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadFeature_FieldsSlice_ProjectsTopLevelKeysAndKeepsRequestedPayloadsWhole()
    {
        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["fields"] = "status,artifacts"
        });

        response["result"]!["isError"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        var feature = JsonNode.Parse(text)!.AsObject();
        feature.Select(pair => pair.Key).ShouldBe(["slug", "status", "artifacts"]);
        feature["artifacts"]!.AsArray().Count.ShouldBe(4);
        text.ShouldContain("frozen by the user");
        text.ShouldNotContain("\"title\"");
    }

    [Fact]
    public async Task ReadFeature_Compact_BodiesBecomeCharsAndArtifactsBecomeRosterRowsWithLastLogLines()
    {
        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["compact"] = true
        });

        response["result"]!["isError"].ShouldBeNull();

        var text = Canned.ToolResultText(response);
        text.ShouldNotContain("Ship the demo");
        text.ShouldNotContain("Wire the API");

        var feature = JsonNode.Parse(text)!.AsObject();
        feature["bodyChars"]!.GetValue<int>().ShouldBe(Canned.StoredLength(Canned.PlanningFeatureJson, "body"));
        feature["slug"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        feature["version"]!.GetValue<int>().ShouldBe(4);

        var rows = feature["artifacts"]!.AsArray();
        rows.Count.ShouldBe(4);

        var spec = rows[0]!.AsObject();
        spec.Select(pair => pair.Key).ShouldBe(["kind", "order", "status", "lastLogLine"]);
        spec["kind"]!.GetValue<string>().ShouldBe("spec");
        spec["status"]!.GetValue<string>().ShouldBe("validated");
        spec["lastLogLine"]!.GetValue<string>().ShouldBe("frozen by the user");

        var task = rows[3]!.AsObject();
        task["lastLogLine"]!.GetValue<string>().ShouldBeEmpty();
        task["blocked"]!.GetValue<bool>().ShouldBeFalse();
        task["blockedBy"]!.ToJsonString().ShouldBe("[1]");
    }

    [Fact]
    public async Task ReadFeature_Compact_ComposesWithSectionsAndRosterQuery()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            Canned.PlanningFeatureSectionedJson);

        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["includeArtifacts"] = false,
            ["sections"] = "goal",
            ["compact"] = true
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");
        ParseQuery(request.Query)["includeArtifacts"].ShouldBe("false");
        ParseQuery(request.Query)["sections"].ShouldBe("goal");

        var feature = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        feature["bodyChars"]!.GetValue<int>().ShouldBe(Canned.StoredLength(Canned.PlanningFeatureSectionedJson, "body"));
        feature["missingSections"]!.ToJsonString().ShouldBe("""["log"]""");
        feature["artifacts"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadFeature_Compact_KeepsTheClaimBlockAsIs()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            """{"slug":"demo-feature","title":"Demo feature","body":"<goal>Ship.</goal>","status":"inProgress","priority":"p1","version":4,"createdAt":"2026-09-08T10:00:00Z","updatedAt":"2026-09-10T08:00:00Z","artifacts":[],"isBlocked":false,"blockedBy":[],"blocks":[],"claimedByAlias":"Dan","claimedByVendor":"qwen","claimedAt":"2026-10-02T10:00:00Z","claimState":"alive","claimedByHuman":false}""");

        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["compact"] = true
        });

        var feature = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        feature["claimedByAlias"]!.GetValue<string>().ShouldBe("Dan");
        feature["claimedByVendor"]!.GetValue<string>().ShouldBe("qwen");
        feature["claimedAt"]!.GetValue<string>().ShouldBe("2026-10-02T10:00:00Z");
        feature["claimState"]!.GetValue<string>().ShouldBe("alive");
        feature["claimedByHuman"]!.GetValue<bool>().ShouldBeFalse();
        feature["isBlocked"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task ReadFeature_Compact_CarriesAHumanHeldClaimFlag()
    {
        Stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            """{"slug":"demo-feature","title":"Demo feature","body":"<goal>Ship.</goal>","status":"inProgress","priority":"p1","version":4,"createdAt":"2026-09-08T10:00:00Z","updatedAt":"2026-09-10T08:00:00Z","artifacts":[],"isBlocked":false,"claimedByAlias":"Dan","claimedByVendor":"qwen","claimedAt":"2026-10-02T10:00:00Z","claimState":"alive","claimedByHuman":true}""");

        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["compact"] = true
        });

        var feature = JsonNode.Parse(Canned.ToolResultText(response))!.AsObject();
        feature.Select(pair => pair.Key).ShouldBe(
            ["slug", "title", "status", "priority", "version", "createdAt", "updatedAt", "bodyChars", "isBlocked",
                "artifacts", "claimedByAlias", "claimedByVendor", "claimedAt", "claimState", "claimedByHuman"]);
        feature["claimedByHuman"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task ListFeatures_WithoutKnobs_ReturnsTodaysPayloadUnchanged()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        JsonNode.DeepEquals(
            JsonNode.Parse(Canned.ToolResultText(response)),
            JsonNode.Parse(Canned.PlanningBoardJson)).ShouldBeTrue();
    }

    [Fact]
    public async Task TaskQueue_WithoutKnobs_ReturnsTodaysPayloadUnchanged()
    {
        var response = await Driver.CallToolAsync("planning_task_queue", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString()
        });

        JsonNode.DeepEquals(
            JsonNode.Parse(Canned.ToolResultText(response)),
            JsonNode.Parse(Canned.PlanningQueueJson)).ShouldBeTrue();
    }

    [Fact]
    public async Task ReadFeature_WithoutKnobs_ReturnsTodaysPayloadUnchanged()
    {
        var response = await Driver.CallToolAsync("planning_read_feature", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug
        });

        var expected = JsonNode.Parse(Canned.PlanningFeatureJson)!;
        foreach (var artifact in expected["artifacts"]!.AsArray()
            .Where(a => a!["kind"]!.GetValue<string>() != "task"))
        {
            artifact!.AsObject().Remove("review");
        }

        JsonNode.DeepEquals(JsonNode.Parse(Canned.ToolResultText(response)), expected).ShouldBeTrue();
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
