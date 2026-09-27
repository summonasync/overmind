using System.Diagnostics;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public abstract class McpSessionTestBase : IAsyncLifetime
{
    protected StubOvermindApi Stub { get; private set; } = default!;

    protected McpProcessDriver Driver { get; private set; } = default!;

    protected TimeSpan StartupToHandshake { get; private set; }

    protected virtual bool AutoHandshake => true;

    public virtual async Task InitializeAsync()
    {
        Stub = StubOvermindApi.Start();
        RegisterRoutes(Stub);

        var watch = Stopwatch.StartNew();
        Driver = McpProcessDriver.Start(Stub.BaseUrl);

        if (AutoHandshake)
        {
            await Driver.InitializeAsync();
        }

        watch.Stop();
        StartupToHandshake = watch.Elapsed;
    }

    protected virtual void RegisterRoutes(StubOvermindApi stub)
    {
    }

    protected static void RegisterDefaultRoutes(StubOvermindApi stub)
    {
        stub.RouteGet("/api/health", Canned.HealthJson);
        stub.RouteGet("/api/projects", Canned.ProjectsJson);
        stub.RouteGet($"/api/projects/{Canned.ProjectId}", Canned.ProjectJson);
        stub.Route("GET", $"/api/projects/{Canned.ProjectId}/entries", req =>
        {
            var type = ParseQuery(req.Query).GetValueOrDefault("type");
            var body = type switch
            {
                "gate" => Canned.GatesJson,
                "guard" => Canned.GuardsJson,
                "note" => Canned.NotesJson,
                "decision" => Canned.DecisionsJson,
                "skill" => Canned.SkillsJson,
                _ => "[]"
            };
            return new StubOvermindApi.Response(200, body);
        });
        stub.RouteGet($"/api/entries/{Canned.EntryId}", Canned.EntryJson);
        stub.Route("POST", "/api/entries", req =>
        {
            var body = JsonNode.Parse(req.Body)!;
            body["id"] = Canned.EntryId.ToString();
            body["currentVersion"] = 1;
            body["createdAt"] = "2026-09-20T10:00:00Z";
            body["updatedAt"] = "2026-09-20T10:00:00Z";
            return new StubOvermindApi.Response(201, body.ToJsonString());
        });
        stub.Route("PUT", $"/api/entries/{Canned.EntryId}", 200, Canned.EntryJson);
        stub.Route("DELETE", "/api/entries/[^/]+", 204, "");
        stub.RouteGet("/api/kits", Canned.KitsWithoutEntryJson);
        stub.RouteGet("/api/search", Canned.SearchJson);
        RegisterNotificationRoutes(stub);
    }

    protected static void RegisterNotificationRoutes(StubOvermindApi stub)
    {
        stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);
        stub.Route("POST", "/api/agent/notifications", 201, Canned.NotificationJson);
        stub.RouteGet("/api/agent/notifications/replies", Canned.RepliesJson);
    }

    protected static void RegisterPlanningRoutes(StubOvermindApi stub)
    {
        stub.RouteGet($"/api/agent/projects/{Canned.ProjectId}/planning/features", Canned.PlanningBoardJson);
        stub.Route(
            "GET",
            $"/api/agent/projects/{Canned.ProjectId}/planning/task-queue",
            req => ParseQuery(req.Query).ContainsKey("limit")
                ? new StubOvermindApi.Response(200, Canned.PlanningQueueHeadJson)
                : new StubOvermindApi.Response(200, Canned.PlanningQueueJson));
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/queue-move",
            200,
            Canned.PlanningQueueItemJson);
        stub.Route(
            "PUT",
            $"/api/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            200,
            Canned.PlanningFeaturePriorityJson);
        stub.Route("POST", $"/api/agent/projects/{Canned.ProjectId}/planning/features", 201, Canned.PlanningFeatureJson);
        stub.Route(
            "POST",
            $"/api/projects/{Canned.ProjectId}/planning/materialize",
            200,
            Canned.PlanningMaterializeReportJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/import",
            200,
            Canned.PlanningImportReportJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/materialization-status",
            Canned.PlanningMaterializeStatusJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            Canned.PlanningFeatureJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/artifacts",
            Canned.PlanningArtifactRosterJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/current",
            Canned.PlanningCurrentTaskJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningStopSlug}/current",
            Canned.PlanningCurrentStopJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningHeldSlug}/current",
            Canned.PlanningCurrentHeldJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningBlockedSlug}/current",
            Canned.PlanningCurrentBlockedStopJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningTaskBlockedSlug}/current",
            Canned.PlanningCurrentTaskBlockedStopJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/dependencies",
            Canned.PlanningDependenciesJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/dependencies",
            200,
            Canned.PlanningDependenciesJson);
        stub.Route(
            "DELETE",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/dependencies/[^/]+",
            200,
            Canned.PlanningDependenciesClearedJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningEmptySlug}/current",
            Canned.PlanningCurrentEmptyJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts",
            req =>
            {
                var submitted = JsonNode.Parse(req.Body)!;
                var artifact = new JsonObject
                {
                    ["id"] = Canned.PlanningTaskTwoId.ToString(),
                    ["kind"] = submitted["kind"]!.GetValue<string>(),
                    ["status"] = "draft",
                    ["review"] = submitted["review"]?.GetValue<string>() ?? "light",
                    ["order"] = submitted["order"]?.GetValue<int>() ?? 0,
                    ["body"] = submitted["body"]!.GetValue<string>(),
                    ["brief"] = submitted["brief"]?.GetValue<string>(),
                    ["frozenAt"] = null,
                    ["version"] = 1,
                    ["createdAt"] = "2026-09-20T10:00:00Z",
                    ["updatedAt"] = "2026-09-20T10:00:00Z",
                    ["log"] = new JsonArray { new JsonObject { ["date"] = "2026-09-20", ["text"] = "stubbed creation log line" } }
                };
                return new StubOvermindApi.Response(201, artifact.ToJsonString());
            });
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts/tasks",
            req =>
            {
                var submitted = JsonNode.Parse(req.Body)!["tasks"]!.AsArray();
                var artifacts = new JsonArray();
                for (var i = 0; i < submitted.Count; i++)
                {
                    var item = submitted[i]!;
                    artifacts.Add(new JsonObject
                    {
                        ["id"] = $"88888888-8888-8888-8888-{i:D12}",
                        ["kind"] = "task",
                        ["status"] = "todo",
                        ["review"] = item["review"]?.GetValue<string>() ?? "light",
                        ["order"] = item["order"]?.GetValue<int>() ?? i,
                        ["body"] = item["body"]!.GetValue<string>(),
                        ["brief"] = item["brief"]?.GetValue<string>(),
                        ["frozenAt"] = null,
                        ["version"] = 1,
                        ["createdAt"] = "2026-09-24T10:00:00Z",
                        ["updatedAt"] = "2026-09-24T10:00:00Z",
                        ["log"] = new JsonArray()
                    });
                }

                var result = new JsonObject
                {
                    ["created"] = submitted.Count,
                    ["updated"] = 0,
                    ["artifacts"] = artifacts
                };
                return new StubOvermindApi.Response(200, result.ToJsonString());
            });
        stub.Route(
            "GET",
            "/api/agent/planning/artifacts/[^/]+",
            req => ParseQuery(req.Query).ContainsKey("sections")
                ? new StubOvermindApi.Response(200, Canned.PlanningArtifactSectionedJson)
                : new StubOvermindApi.Response(200, Canned.PlanningArtifactReadJson));
        stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/status", 200, Canned.PlanningArtifactStatusEchoJson);
        stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/log", 200, Canned.PlanningArtifactLogEchoJson);
        stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/validate",
            req =>
            {
                var submitted = JsonNode.Parse(req.Body)!;
                var lastLogLine = submitted["asMigrated"]?.GetValue<bool>() == true
                    ? $"validated as migrated — pre-approved on disk: {submitted["logNote"]!.GetValue<string>()}"
                    : "validated with human proof: notification 13131313-1313-1313-1313-131313131313 replied 2026-09-26 10:00:00 UTC";
                var echo = new JsonObject
                {
                    ["id"] = Canned.PlanningSpecArtifactId.ToString(),
                    ["status"] = "validated",
                    ["version"] = 3,
                    ["lastLogLine"] = lastLogLine
                };
                return new StubOvermindApi.Response(200, echo.ToJsonString());
            });
        stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/patch",
            req =>
            {
                var submitted = JsonNode.Parse(req.Body)!;
                var result = new JsonObject
                {
                    ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
                    ["kind"] = "task",
                    ["section"] = submitted["section"]?.GetValue<string>() is { Length: > 0 } section
                        ? section
                        : "acceptance-criteria",
                    ["version"] = 3,
                    ["bodyChars"] = 412,
                    ["status"] = "inProgress",
                    ["lastLogLine"] = "patch recorded on the board"
                };
                return new StubOvermindApi.Response(200, result.ToJsonString());
            });
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/current",
            200,
            Canned.PlanningFeatureMutationEchoJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/status",
            200,
            Canned.PlanningFeatureMutationEchoJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/update",
            200,
            Canned.PlanningFeatureUpdateEchoJson);
    }

    protected static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx > 0)
            {
                result[Uri.UnescapeDataString(pair[..idx])] = Uri.UnescapeDataString(pair[(idx + 1)..]);
            }
        }

        return result;
    }

    protected StubOvermindApi.Request FindRequest(string method, string pathPrefix)
    {
        return Stub.Requests.Single(r => r.Method == method && r.Path.StartsWith(pathPrefix, StringComparison.Ordinal));
    }

    public virtual async Task DisposeAsync()
    {
        Driver.Dispose();
        await Driver.Completion;
        Driver.AssertStdoutPurity();
        await Stub.DisposeAsync();
    }
}
