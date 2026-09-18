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
            return new StubOvermindApi.Response(201, body.ToJsonString());
        });
        stub.Route("PUT", $"/api/entries/{Canned.EntryId}", 200, Canned.EntryJson);
        stub.RouteGet("/api/search", Canned.SearchJson);
    }

    protected static void RegisterPlanningRoutes(StubOvermindApi stub)
    {
        stub.RouteGet($"/api/agent/projects/{Canned.ProjectId}/planning/features", Canned.PlanningBoardJson);
        stub.Route("POST", $"/api/agent/projects/{Canned.ProjectId}/planning/features", 201, Canned.PlanningFeatureJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}",
            Canned.PlanningFeatureJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/current",
            Canned.PlanningCurrentTaskJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningStopSlug}/current",
            Canned.PlanningCurrentStopJson);
        stub.RouteGet(
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningEmptySlug}/current",
            Canned.PlanningCurrentEmptyJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts",
            201,
            Canned.PlanningTaskTwoJson);
        stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/status", 200, "");
        stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/log", 200, "");
        stub.Route("POST", "/api/agent/planning/artifacts/[^/]+/validate", 200, "");
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/current",
            200,
            Canned.PlanningFeatureJson);
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
