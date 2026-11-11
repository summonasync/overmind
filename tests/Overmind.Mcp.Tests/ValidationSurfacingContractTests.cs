using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ValidationSurfacingContractTests : McpSessionTestBase
{
    private static readonly string UpdatePath =
        $"/api/agent/projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/update";

    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task PlanningUpdateFeature_ValidationProblem_SurfacesFieldMessagesFromErrorsDict()
    {
        Stub.Route(
            "POST",
            UpdatePath,
            400,
            """
            {"title":"One or more validation errors occurred.","status":400,
             "errors":{"LogNote":["'Log Note' must not be empty."]}}
            """);

        var response = await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "surface probe"
        });

        var text = ToolErrorText(response);
        text.ShouldContain("LogNote: 'Log Note' must not be empty.");
        text.ShouldNotContain("\"errors\"");
        text.ShouldNotContain("{");
        Driver.HasExited.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidationSurfacing_IsDeterministicAndSortedAcrossCalls()
    {
        Stub.Route(
            "POST",
            UpdatePath,
            400,
            """
            {"title":"One or more validation errors occurred.","status":400,
             "errors":{"Priority":["Must be one of: p0, p1, p2, p3."],
                       "LogNote":["'Log Note' must not be empty.","Provide a note describing why the edit happened."]}}
            """);

        var first = ToolErrorText(await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "determinism probe",
            ["priority"] = "p9"
        }));
        var second = ToolErrorText(await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["logNote"] = "determinism probe",
            ["priority"] = "p9"
        }));

        second.ShouldBe(first);
        first.ShouldContain("overmind API returned 400 (POST " + UpdatePath + "): ");
        first.IndexOf("LogNote: ", StringComparison.Ordinal).ShouldBeLessThan(first.IndexOf("Priority: ", StringComparison.Ordinal));
        first.IndexOf("Provide a note", StringComparison.Ordinal).ShouldBeGreaterThan(first.IndexOf("'Log Note' must not be empty.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlanningUpdateFeature_BadRequestWithoutErrorsDict_KeepsTitleOnlyText()
    {
        Stub.Route(
            "POST",
            UpdatePath,
            400,
            """{"title":"InvalidFeaturePriority","detail":"Invalid feature priority 'p9'. Expected one of: p0, p1, p2, p3.","status":400}""");

        var response = await Driver.CallToolAsync("planning_update_feature", new JsonObject
        {
            ["slug"] = Canned.PlanningSlug,
            ["priority"] = "p9",
            ["logNote"] = "no-dict probe"
        });

        var text = ToolErrorText(response);
        text.ShouldBe(
            "An error occurred invoking 'planning_update_feature': overmind API returned 400 (POST " +
            UpdatePath + "): Invalid feature priority 'p9'. Expected one of: p0, p1, p2, p3.");
        text.ShouldNotContain("Errors:");
        Driver.HasExited.ShouldBeFalse();
    }

    private static string ToolErrorText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        result!["isError"]!.GetValue<bool>().ShouldBeTrue("expected a tool error, got: " + response.ToJsonString());
        return result["content"]!.AsArray().First()!["text"]!.GetValue<string>();
    }
}
