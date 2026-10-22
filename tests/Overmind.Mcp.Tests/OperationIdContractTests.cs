using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class OperationIdContractTests : McpSessionTestBase
{
    private static readonly Guid OperationId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string ConflictProblemJson =
        """{"title":"OperationIdConflict","status":409,"detail":"Operation id '44444444-4444-4444-4444-444444444444' was already used with a different request (method, path or body differ). An operation id replays only its own request: resend the identical payload to get the stored response, or mint a new id for a different operation."}""";

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task SetStatus_WithoutOperationId_SendsNoHeader()
    {
        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "move without an idempotency key",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString()
        });

        response["error"].ShouldBeNull();
        var request = FindRequest("POST", "/api/agent/planning/artifacts/");
        request.Headers.ContainsKey("X-Operation-Id").ShouldBeFalse();
    }

    [Fact]
    public async Task SetStatus_WithOperationId_SendsTheHeaderVerbatim()
    {
        await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "move with an idempotency key",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["operationId"] = OperationId.ToString()
        });

        var request = FindRequest("POST", "/api/agent/planning/artifacts/");
        request.Headers["X-Operation-Id"].ShouldBe(OperationId.ToString("D"));
    }

    [Fact]
    public async Task UpsertArtifact_ReplayedEcho_SurfacesTheReplayedFlag()
    {
        Stub.Route(
            "POST",
            $"/api/agent/projects/{Canned.ProjectId}/planning/features/[^/]+/artifacts",
            200,
            """{"id":"99999999-9999-9999-9999-999999999999","kind":"task","status":"todo","order":0,"body":"<goal>x</goal>","frozenAt":null,"version":1,"createdAt":"2026-10-22T10:00:00Z","updatedAt":"2026-10-22T10:00:00Z","log":[],"replayed":true}""");

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = "demo-feature",
            ["kind"] = "task",
            ["body"] = "<goal>x</goal>"
        });

        Canned.ToolResultText(response).ShouldContain("\"replayed\":true");
    }

    [Fact]
    public async Task ApiDown_ReadTool_RendersTheRetryableMarker()
    {
        await Driver.CallToolAsync("list_projects", new JsonObject());
        Stub.Stop();

        var response = await Driver.CallToolAsync("list_projects", new JsonObject());

        var text = ToolErrorText(response);
        text.ShouldContain(OvermindApiClient.RetryableMarker);
    }

    [Fact]
    public async Task ApiDown_MutationTool_RendersTheRetryableMarkerWithUnknownOutcome()
    {
        await Driver.CallToolAsync("list_projects", new JsonObject());
        Stub.Stop();

        var response = await Driver.CallToolAsync("planning_upsert_artifact", new JsonObject
        {
            ["projectId"] = Canned.ProjectId.ToString(),
            ["featureSlug"] = "demo-feature",
            ["kind"] = "task",
            ["body"] = "<goal>probe</goal>"
        });

        var text = ToolErrorText(response);
        text.ShouldContain(OvermindApiClient.RetryableMarker);
        text.ShouldContain("the outcome is unknown");
    }

    [Fact]
    public async Task BusinessRejection_NeverCarriesTheRetryableMarker()
    {
        Stub.Route(
            "POST",
            "/api/agent/planning/artifacts/[^/]+/status",
            req => new StubOvermindApi.Response(409, ConflictProblemJson));

        var response = await Driver.CallToolAsync("planning_set_status", new JsonObject
        {
            ["status"] = "review",
            ["logNote"] = "conflicting move",
            ["artifactId"] = Canned.PlanningTaskTwoId.ToString(),
            ["operationId"] = OperationId.ToString()
        });

        var text = ToolErrorText(response);
        text.ShouldContain("was already used with a different request");
        text.ShouldNotContain(OvermindApiClient.RetryableMarker);
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"]!;
        result["isError"]!.GetValue<bool>().ShouldBeTrue();
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
