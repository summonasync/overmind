using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

// 266/T3 row 5 — the two not-found messages the crew paid for in full payloads: a slug rebuilt from
// memory, and a project id copied out of an old note. The vocabulary reads live adapter-side (the
// board list and the project list), so the candidate rides the error the caller already has.
public class NearMissHintContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);

        stub.RouteGet($"/api/agent/projects/{Canned.ProjectId}/planning/features", BoardJson);
        stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/status",
            404,
            """{"title":"MissingFeature","detail":"Feature 'demo-featur' was not found."}""");
        stub.Route(
            "GET",
            $"/api/agent/projects/{MissingProjectId}/planning/features",
            404,
            """{"title":"MissingProject","detail":"Project '6f990fbe-0000-0000-0000-000000000000' was not found."}""");
        stub.Route(
            "POST",
            $"/api/agent/projects/{AmbiguousProject}/planning/features/[^/]+/status",
            404,
            """{"title":"MissingFeature","detail":"Feature 'feat' was not found."}""");
        stub.RouteGet($"/api/agent/projects/{AmbiguousProject}/planning/features", AmbiguousBoardJson);
    }

    private static readonly Guid MissingProjectId = Guid.Parse("6f990fbe-0000-0000-0000-000000000000");
    private static readonly Guid AmbiguousProject = Guid.Parse("5e5e5e5e-5e5e-5e5e-5e5e-5e5e5e5e5e5e");

    private const string BoardJson =
        """
        [{"slug":"demo-feature","title":"Demo","status":"draft","priority":"p2","version":1,"taskTotal":0,"taskDone":0},
         {"slug":"251-global-materialization-v2","title":"Global","status":"draft","priority":"p2","version":1,"taskTotal":0,"taskDone":0}]
        """;

    private const string AmbiguousBoardJson =
        """
        [{"slug":"feat-one","title":"One","status":"draft","priority":"p2","version":1,"taskTotal":0,"taskDone":0},
         {"slug":"feat-two","title":"Two","status":"draft","priority":"p2","version":1,"taskTotal":0,"taskDone":0}]
        """;

    [Fact]
    public async Task SlugNotFound_NearestBoardSlugIsOffered()
    {
        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["slug"] = "demo-featur",
            ["status"] = "done",
            ["logNote"] = "closing blind",
            ["projectId"] = Canned.ProjectId.ToString(),
        });

        var text = ErrorText(response);
        text.ShouldContain("Feature 'demo-featur' was not found.");
        text.ShouldContain("Did you mean 'demo-feature'?");
    }

    [Fact]
    public async Task SlugNotFound_LongDerivedSlugVsMemoryRebuild_StillLandsTheCandidate()
    {
        // The survey case: the crew writes '251-global-materialization' for the 64-char derived slug.
        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["slug"] = "251-global-materialization",
            ["status"] = "done",
            ["logNote"] = "closing blind",
            ["projectId"] = Canned.ProjectId.ToString(),
        });

        ErrorText(response).ShouldContain("Did you mean '251-global-materialization-v2'?");
    }

    [Fact]
    public async Task SlugNotFound_TwoEquallyNear_IsRefusedWithoutAGuess()
    {
        var response = await Driver.CallToolAsync("planning_set_feature_status", new JsonObject
        {
            ["slug"] = "feat-zer",
            ["status"] = "done",
            ["logNote"] = "closing blind",
            ["projectId"] = AmbiguousProject.ToString(),
        });

        var text = ErrorText(response);
        text.ShouldNotContain("Did you mean");
        text.ShouldContain("planning_list_features shows the 2 slugs this project actually has");
    }

    [Fact]
    public async Task ProjectNotFound_RegisteredAliasesRideTheMessage()
    {
        var response = await Driver.CallToolAsync("planning_list_features", new JsonObject
        {
            ["projectId"] = MissingProjectId.ToString(),
        });

        var text = ErrorText(response);
        text.ShouldContain("Project '6f990fbe-0000-0000-0000-000000000000' was not found.");
        text.ShouldContain("Registered projects: alpha (11111111-1111-1111-1111-111111111111), " +
            "beta (22222222-2222-2222-2222-222222222222)");
        text.ShouldContain("list_projects shows paths and descriptions");
    }

    private static string ErrorText(JsonNode response)
    {
        response["error"].ShouldBeNull();
        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
