using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class UpsertLintContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task UpsertArtifact_MalformedPointerLine_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "<goal>Wire it.</goal>\n\n<refs>\npointer: src/Auth/Keys.cs (see below)\n</refs>"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("body: malformed pointer line");
        text.ShouldContain("'pointer: <repo-relative-path>[#anchor]'");
        text.ShouldContain("Example: 'pointer: src/Overmind.Core/Entities/PlanningArtifact.cs#L81'");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" &&
            (r.Path.EndsWith("/artifacts", StringComparison.Ordinal) ||
             r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UpsertTasks_MalformedPointerLineInItem_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray(
                new JsonObject
                {
                    ["body"] = "<goal>First.</goal>\npointer: docs/plan.md#notes (extra words)",
                    ["order"] = 0
                })
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("tasks[0]: body: malformed pointer line");
        text.ShouldContain("Example:");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" &&
            (r.Path.EndsWith("/artifacts", StringComparison.Ordinal) ||
             r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UpsertTasks_ItemsAtTopLevelInsteadOfArray_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonObject
            {
                ["body"] = "<goal>Wire it.</goal>",
                ["order"] = 0
            }
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("tasks must be a JSON array of task items");
        text.ShouldContain("item fields never sit at the call's top level");
        text.ShouldContain("<goal>Fix the login redirect.</goal>");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" &&
            (r.Path.EndsWith("/artifacts", StringComparison.Ordinal) ||
             r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UpsertTasks_BlankBodyInItem_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["tasks"] = new JsonArray(new JsonObject { ["order"] = 3 })
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("tasks[0]: body is required");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" &&
            (r.Path.EndsWith("/artifacts", StringComparison.Ordinal) ||
             r.Path.EndsWith("/artifacts/tasks", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UpsertArtifact_ValidPointerLine_StillReachesTheWire()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "<goal>Wire it.</goal>\n\n<refs>\npointer: src/Auth/Keys.cs#rotation\n</refs>"
        });

        response["result"]!["isError"].ShouldBeNull();
        Stub.Requests.Count(r =>
            r.Method == "POST" &&
            r.Path.EndsWith("/artifacts", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_DryRun_ReachesTheWireMarkedAndEchoesDryRun()
    {
        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "<goal>Dry.</goal>",
            ["dryRun"] = true
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("POST", $"/api/agent/projects/{Canned.ProjectId}/planning/features/");
        request.Body.ShouldContain("\"dryRun\":true");

        var text = Canned.ToolResultText(response);
        text.ShouldContain("\"dryRun\":true");
    }

    [Fact]
    public async Task UpsertTasks_DryRun_ReachesTheWireMarkedAndEchoesDryRun()
    {
        var response = await Driver.CallToolAsync("planning_upsert_tasks", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["dryRun"] = true,
            ["tasks"] = new JsonArray(new JsonObject { ["body"] = "<goal>Dry.</goal>", ["order"] = 0 })
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = FindRequest("POST", $"/api/agent/projects/{Canned.ProjectId}/planning/features/");
        request.Body.ShouldContain("\"dryRun\":true");

        Canned.ToolResultText(response).ShouldContain("\"dryRun\":true");
    }
}
