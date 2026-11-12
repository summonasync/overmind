using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class PlanningBatchToolContractTests : McpSessionTestBase
{
    private const string BatchResponseJson =
        """
        {
          "featureSlug": "demo-feature",
          "results": [
            {"op":"patch","index":0,"payload":{"artifactId":"99999999-9999-9999-9999-999999999999","kind":"task","section":"acceptance-criteria","version":3,"bodyChars":412,"status":"inProgress","lastLogLine":"patch recorded on the board","tickedIndex":1,"tickedText":"expose the endpoints"}},
            {"op":"append_log","index":1,"payload":{"id":"99999999-9999-9999-9999-999999999999","status":"inProgress","version":3,"lastLogLine":"log line recorded on the board"}},
            {"op":"set_status","index":2,"payload":{"id":"99999999-9999-9999-9999-999999999999","status":"done","version":4,"lastLogLine":"status move recorded on the board","featureStatusChanged":"inProgress → review"}},
            {"op":"upsert_tasks","index":3,"payload":{"created":1,"updated":0,"artifacts":[{"id":"88888888-8888-8888-8888-000000000000","kind":"task","status":"todo","review":"light","order":5,"body":"# Late\n\nlate task body\n","brief":null,"frozenAt":null,"version":1,"createdAt":"2026-11-12T10:00:00Z","updatedAt":"2026-11-12T10:00:00Z","log":[]}]}},
            {"op":"notify","index":4,"payload":{"id":"13131313-1313-1313-1313-131313131313","projectId":"11111111-1111-1111-1111-111111111111","kind":"question","featureSlug":"demo-feature","tab":"tasks","text":"ship it?","agent":"coder","createdAt":"2026-11-12T10:00:00Z","readAt":null,"replyText":null,"repliedAt":null,"url":"http://127.0.0.1:5183/dashboard?project=11111111-1111-1111-1111-111111111111&feature=demo-feature&tab=tasks"}}
          ],
          "warnings": ["post-commit materialization dispatch failed: simulated"]
        }
        """;

    private static readonly Guid ReplayId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/batch",
            200,
            BatchResponseJson);
    }

    [Fact]
    public async Task Batch_UnknownOpName_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["ops"] = new JsonArray(new JsonObject
            {
                ["op"] = "append-log",
                ["alias"] = "T2",
                ["note"] = "closing"
            })
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("Unknown op 'append-log'");
        text.ShouldContain("patch, append_log, set_status, upsert_tasks, notify");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_MissingRequiredParams_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["ops"] = new JsonArray(new JsonObject
            {
                ["op"] = "set_status",
                ["alias"] = "T2"
            })
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("ops[0]:");
        text.ShouldContain("set_status requires a non-blank status string");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_PatchWithoutArtifactAddress_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["ops"] = new JsonArray(new JsonObject
            {
                ["op"] = "patch",
                ["patch"] = new JsonObject { ["op"] = "tick_ac", ["acIndex"] = 1 }
            })
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("ops[0]:");
        text.ShouldContain("patch requires the artifact to mutate");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_UpsertTasksWithoutTasks_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "upsert_tasks" })
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();

        var text = Canned.ToolResultText(response);
        text.ShouldContain("ops[0]:");
        text.ShouldContain("upsert_tasks requires a non-empty tasks[]");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_OpsNotAnArray_RejectedBeforeAnyHttp()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["ops"] = new JsonObject { ["op"] = "append_log", ["alias"] = "T2", ["note"] = "closing" }
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("ops must be a JSON array of op objects");

        Stub.Requests.ShouldNotContain(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_HappyPath_MapsRequestRidesReplayHeaderAndProjectsOneCompactRowPerOpInOrder()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["operationId"] = ReplayId.ToString(),
            ["ops"] = new JsonArray(
                new JsonObject
                {
                    ["op"] = "patch",
                    ["alias"] = $"{Canned.PlanningSlug}/T0",
                    ["patch"] = new JsonObject { ["op"] = "tick_ac", ["acIndex"] = 1, ["logNote"] = "ticked" }
                },
                new JsonObject
                {
                    ["op"] = "append_log",
                    ["alias"] = "T0",
                    ["note"] = "closing"
                },
                new JsonObject
                {
                    ["op"] = "set_status",
                    ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
                    ["status"] = "done",
                    ["logNote"] = "task closed"
                },
                new JsonObject
                {
                    ["op"] = "upsert_tasks",
                    ["tasks"] = new JsonArray(new JsonObject
                    {
                        ["body"] = "<goal>Late.</goal>",
                        ["order"] = 5
                    })
                },
                new JsonObject
                {
                    ["op"] = "notify",
                    ["kind"] = "question",
                    ["text"] = "ship it?",
                    ["tab"] = "tasks"
                })
        });

        response["result"]!["isError"].ShouldBeNull();

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/batch");
        request.Headers["X-Operation-Id"].ShouldBe(ReplayId.ToString("D"));

        var body = JsonNode.Parse(request.Body)!;
        var ops = body["ops"]!.AsArray();
        ops.Count.ShouldBe(5);
        ops[0]!["alias"]!.GetValue<string>().ShouldBe($"{Canned.PlanningSlug}/T0");
        ops[0]!["patch"]!["op"]!.GetValue<string>().ShouldBe("tick_ac");
        ops[0]!["patch"]!["logNote"]!.GetValue<string>().ShouldBe("ticked");
        ops[1]!["note"]!.GetValue<string>().ShouldBe("closing");
        ops[2]!["artifactId"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        ops[2]!["status"]!.GetValue<string>().ShouldBe("done");
        ops[2]!["logNote"]!.GetValue<string>().ShouldBe("task closed");
        ops[3]!["tasks"]![0]!["body"]!.GetValue<string>().ShouldContain("<goal>Late.</goal>");
        ops[3]!["tasks"]![0]!["order"]!.GetValue<int>().ShouldBe(5);
        ops[4]!["kind"]!.GetValue<string>().ShouldBe("question");
        ops[4]!["text"]!.GetValue<string>().ShouldBe("ship it?");
        ops[4]!["tab"]!.GetValue<string>().ShouldBe("tasks");

        var text = Canned.ToolResultText(response);
        var batch = JsonNode.Parse(text)!.AsObject();
        batch["featureSlug"]!.GetValue<string>().ShouldBe(Canned.PlanningSlug);
        batch["warnings"]!.AsArray().Count.ShouldBe(1);

        var rows = batch["results"]!.AsArray();
        rows.Count.ShouldBe(5);
        rows.Select(r => r!["op"]!.GetValue<string>()).ShouldBe(
            ["patch", "append_log", "set_status", "upsert_tasks", "notify"]);
        rows.Select(r => r!["index"]!.GetValue<int>()).ShouldBe([0, 1, 2, 3, 4]);

        var patchRow = rows[0]!.AsObject();
        patchRow["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        patchRow["section"]!.GetValue<string>().ShouldBe("acceptance-criteria");
        patchRow["tickedIndex"]!.GetValue<int>().ShouldBe(1);
        patchRow["tickedText"]!.GetValue<string>().ShouldBe("expose the endpoints");
        patchRow["version"]!.GetValue<int>().ShouldBe(3);

        var logRow = rows[1]!.AsObject();
        logRow["id"]!.GetValue<string>().ShouldBe(Canned.PlanningTaskTwoId.ToString());
        logRow["lastLogLine"]!.GetValue<string>().ShouldBe("log line recorded on the board");

        var statusRow = rows[2]!.AsObject();
        statusRow["status"]!.GetValue<string>().ShouldBe("done");
        statusRow["featureStatusChanged"]!.GetValue<string>().ShouldBe("inProgress → review");

        var tasksRow = rows[3]!.AsObject();
        tasksRow["created"]!.GetValue<int>().ShouldBe(1);
        tasksRow["updated"]!.GetValue<int>().ShouldBe(0);
        var taskRow = tasksRow["tasks"]!.AsArray().Single()!.AsObject();
        taskRow["id"]!.GetValue<string>().ShouldBe("88888888-8888-8888-8888-000000000000");
        taskRow["order"]!.GetValue<int>().ShouldBe(5);
        taskRow["bodyChars"]!.GetValue<int>().ShouldBe(23);
        text.ShouldNotContain("late task body");

        var notifyRow = rows[4]!.AsObject();
        notifyRow["id"]!.GetValue<string>().ShouldBe(Canned.NotificationId.ToString());
        notifyRow["kind"]!.GetValue<string>().ShouldBe("question");
        notifyRow["url"]!.GetValue<string>().ShouldContain("feature=demo-feature");
    }

    [Fact]
    public async Task Batch_OmittedProjectId_ResolvesThroughOnboardLikeSiblingPlanningTools()
    {
        var response = await Driver.CallToolAsync("planning_batch", new JsonObject
        {
            ["featureSlug"] = Canned.PlanningSlug,
            ["ops"] = new JsonArray(new JsonObject
            {
                ["op"] = "append_log",
                ["alias"] = "T2",
                ["note"] = "closing"
            })
        });

        response["result"]!["isError"].ShouldBeNull();

        var onboard = Stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var onboardBody = JsonNode.Parse(onboard.Body)!;
        onboardBody["create"]!.GetValue<bool>().ShouldBeFalse();

        var request = Stub.Requests.Single(r =>
            r.Method == "POST" && r.Path.EndsWith("/batch", StringComparison.Ordinal));
        request.Path.ShouldBe($"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/batch");
    }
}
