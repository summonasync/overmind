using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class MutationTimeoutContractTests : McpSessionTestBase
{
    private const int HangMilliseconds = 15_000;

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task SetStatus_Timeout_LogNoteEndsTheLastLogLine_AnswersCommittedAtVersion()
    {
        HangPost("/api/agent/planning/artifacts/[^/]+/status");
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, SectionedArtifact(
                    7,
                    "- 2026-10-01 [agent] — some earlier note",
                    "- 2026-10-05 [agent] — review pass recorded after the timeout drill"))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "review pass recorded after the timeout drill",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("committed at v7 — do not resend");
        text.ShouldContain($"artifact {Canned.PlanningTaskTwoId}");

        Stub.Requests.Count(r => r.Method == "GET" && r.Query.Contains("sections=log")).ShouldBe(1);
    }

    [Fact]
    public async Task SetStatus_Timeout_LogNoteAbsentFromLog_AnswersNoTrace()
    {
        HangPost("/api/agent/planning/artifacts/[^/]+/status");
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, SectionedArtifact(
                    7,
                    "- 2026-10-01 [agent] — some earlier note"))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "review pass recorded after the timeout drill",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("no trace — resend safe");
    }

    [Fact]
    public async Task SetStatus_Timeout_MultilineLogNote_AnswersCommittedAtVersion()
    {
        HangPost("/api/agent/planning/artifacts/[^/]+/status");
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, SectionedArtifact(
                    7,
                    "- 2026-10-01 [agent] — some earlier note",
                    "- 2026-10-05 [agent] — multiline note line one",
                    "line two of the note"))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "multiline note line one\nline two of the note",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("committed at v7 — do not resend");
    }

    [Fact]
    public async Task SetStatus_Timeout_ReadBackHasNoLogSection_AnswersUnknownInsteadOfNoTrace()
    {
        HangPost("/api/agent/planning/artifacts/[^/]+/status");
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, EmptyBodyArtifact(7))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "review pass recorded after the timeout drill",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("unknown — read before resending");
        text.ShouldContain("no readable log section");
    }

    [Fact]
    public async Task SetStatus_Timeout_InlineLogWrapper_AnswersCommittedAtVersion()
    {
        HangPost("/api/agent/planning/artifacts/[^/]+/status");
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, InlineLogArtifact(
                    7,
                    "<log>- 2026-10-05 [agent] — inline wrapper drill note</log>"))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "inline wrapper drill note",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("committed at v7 — do not resend");
    }

    [Fact]
    public async Task SetStatus_Timeout_ProbeReadFails_AnswersUnknown()
    {
        HangPost("/api/agent/planning/artifacts/[^/]+/status");
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            404,
            Canned.PlanningArtifactNotFoundJson);

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "review pass recorded after the timeout drill",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("unknown — read before resending");
    }

    [Fact]
    public async Task UpsertArtifact_Timeout_WithoutLogNote_AnswersUnknownWithoutProbe()
    {
        HangPost($"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts");

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["body"] = "# Task body"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("unknown — read before resending");

        Stub.Requests.Any(r =>
            r.Method == "GET" &&
            r.Path.StartsWith("/api/agent/planning/artifacts", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertArtifact_Timeout_LogNoteEndsTheNewestLog_AnswersCommittedAtVersion()
    {
        HangPost($"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts");
        RegisterRosterRoute();
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, SectionedArtifact(
                    7,
                    "- 2026-10-01 [agent] — some earlier note",
                    "- 2026-10-05 [agent] — upsert written during the timeout drill"))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["order"] = 3,
            ["body"] = "# Task body",
            ["logNote"] = "upsert written during the timeout drill"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("committed at v7 — do not resend");
        text.ShouldContain($"the task at order 3 of '{Canned.PlanningSlug}'");

        Stub.Requests.Count(r =>
            r.Method == "GET" &&
            r.Path.EndsWith("/artifacts", StringComparison.Ordinal) &&
            r.Query.Contains("kind=task")).ShouldBe(1);
    }

    [Fact]
    public async Task UpsertArtifact_Timeout_LogNoteAbsentFromEveryCandidate_AnswersNoTrace()
    {
        HangPost($"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts");
        RegisterRosterRoute();
        Stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => req.Query.Contains("sections=log")
                ? new StubOvermindApi.Response(200, SectionedArtifact(
                    7,
                    "- 2026-10-01 [agent] — some earlier note"))
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["order"] = 3,
            ["body"] = "# Task body",
            ["logNote"] = "upsert written during the timeout drill"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("no trace — resend safe");
    }

    [Fact]
    public async Task UpsertArtifact_Timeout_NothingAtTheWantedOrder_AnswersUnknown()
    {
        HangPost($"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts");

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = Canned.PlanningSlug,
            ["kind"] = "task",
            ["order"] = 9,
            ["body"] = "# Task body",
            ["logNote"] = "upsert written during the timeout drill"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        Canned.ToolResultText(response).ShouldContain("unknown — read before resending");
    }

    [Fact]
    public async Task SetFeatureStatus_Timeout_ProbeFindsTheNote_AnswersCommittedAtVersion()
    {
        HangPost($"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/status");
        Stub.Route(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+",
            req => req.Query.Contains("includeArtifacts=false")
                ? new StubOvermindApi.Response(200, FeatureWithLog(
                    9,
                    "- 2026-10-05 [agent] — closing after the timeout drill"))
                : new StubOvermindApi.Response(200, Canned.PlanningFeatureJson));

        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["slug"] = Canned.PlanningSlug,
            ["status"] = "done",
            ["logNote"] = "closing after the timeout drill"
        });

        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        var text = Canned.ToolResultText(response);
        text.ShouldContain("committed at v9 — do not resend");
        text.ShouldContain($"feature '{Canned.PlanningSlug}'");

        Stub.Requests.Count(r => r.Method == "GET" && r.Query.Contains("includeArtifacts=false")).ShouldBe(1);
    }

    private void HangPost(string pathPattern) =>
        Stub.Route("POST", pathPattern, _ =>
        {
            Thread.Sleep(HangMilliseconds);
            return new StubOvermindApi.Response(200, "{}");
        });

    private void RegisterRosterRoute() =>
        Stub.Route(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts",
            200,
            new JsonObject
            {
                ["artifacts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
                        ["kind"] = "task",
                        ["status"] = "inProgress",
                        ["version"] = 6,
                        ["order"] = 3,
                        ["updatedAt"] = "2026-10-05T10:00:00Z",
                        ["pointers"] = new JsonArray()
                    }
                }
            }.ToJsonString());

    private static string SectionedArtifact(int version, params string[] logLines) =>
        LogPayload(logLines, version, ("kind", "task"), ("status", "review"));

    private static string InlineLogArtifact(int version, string bodyLine)
    {
        var payload = new JsonObject
        {
            ["id"] = Canned.PlanningTaskTwoId.ToString(),
            ["version"] = version,
            ["body"] = bodyLine
        };
        return payload.ToJsonString();
    }

    private static string EmptyBodyArtifact(int version)
    {
        var payload = new JsonObject
        {
            ["id"] = Canned.PlanningTaskTwoId.ToString(),
            ["version"] = version,
            ["body"] = string.Empty
        };
        return payload.ToJsonString();
    }

    private static string FeatureWithLog(int version, params string[] logLines) =>
        LogPayload(logLines, version, ("slug", Canned.PlanningSlug), ("status", "inProgress"));

    private static string LogPayload(string[] logLines, int version, params (string Key, string Value)[] extras)
    {
        var payload = new JsonObject
        {
            ["id"] = Canned.PlanningTaskTwoId.ToString(),
            ["version"] = version,
            ["body"] = "<log>\n" + string.Join("\n", logLines) + "\n</log>"
        };
        foreach (var (key, value) in extras)
        {
            payload[key] = value;
        }

        return payload.ToJsonString();
    }
}
