using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

// 266 T4. The host rides the one-line remedy as the "fix" ProblemDetails extension; the adapter must
// carry it into the tool-error text or the agent-visible half of the contract dies at the wire.
public class ConflictFixSurfacingContractTests : McpSessionTestBase
{
    private static readonly string UpdatePath =
        $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/update";

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task Conflict409_WithFixExtension_SurfacesTheFixLineAfterTheDetail()
    {
        Stub.Route(
            "POST",
            UpdatePath,
            409,
            """
            {"title":"FeatureClaimHeld","status":409,
             "detail":"Feature 'x': claim held by Dan by qwen (alive). The claim acts as a mutation lock.",
             "fix":"Coordinate with Dan by qwen in the feature's lane wave room and have the holder release via planning_unclaim_feature, or pick another feature — reads stay open to you."}
            """);

        var text = ToolErrorText(await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "fix surfacing probe",
        }));

        text.ShouldContain("overmind API returned 409 (POST " + UpdatePath + "): ");
        text.ShouldContain("claim held by Dan by qwen");
        text.ShouldContain(". Fix: Coordinate with Dan by qwen");
        text.ShouldNotContain(".. Fix:"); // the detail already ends with a period — no stacked dot
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task Conflict409_WithoutFixExtension_TextStaysAsBefore()
    {
        Stub.Route(
            "POST",
            UpdatePath,
            409,
            """
            {"title":"OperationIdConflict","status":409,
             "detail":"The operation id was already used with a different request."}
            """);

        var text = ToolErrorText(await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "no-fix probe",
        }));

        text.ShouldContain("The operation id was already used with a different request.");
        text.ShouldNotContain("Fix:");
    }

    [Fact]
    public async Task StopGate409_KeepsStopLeadingAndCarriesTheFix()
    {
        Stub.Route(
            "POST",
            UpdatePath,
            409,
            """
            {"title":"HumanValidationRequired","status":409,
             "detail":"STOP: the plan of feature 'x' awaits human validation — ask the user to approve it in the Overmind UI.",
             "fix":"Ask the human to validate the plan of feature x in the Overmind UI, then re-read it before continuing."}
            """);

        var text = ToolErrorText(await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "stop gate probe",
        }));

        text.ShouldContain("STOP: the plan of feature 'x' awaits human validation");
        text.ShouldContain(". Fix: Ask the human to validate");
        text.IndexOf("STOP:", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf(". Fix:", StringComparison.Ordinal));
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
