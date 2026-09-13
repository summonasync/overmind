using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class PlanningResourceContractTests : McpSessionTestBase
{
    protected override void RegisterRoutes(StubOvermindApi stub)
    {
        RegisterDefaultRoutes(stub);
        RegisterPlanningRoutes(stub);
    }

    [Fact]
    public async Task ResourceTemplates_ContainTheThreePlanningUris()
    {
        var response = await Driver.CallAsync("resources/templates/list", new JsonObject());

        var templates = response["result"]!["resourceTemplates"]!.AsArray()
            .Select(t => t!["uriTemplate"]!.GetValue<string>())
            .ToArray();

        templates.ShouldContain("overmind://projects/{id}/planning/features");
        templates.ShouldContain("overmind://projects/{id}/planning/features/{slug}");
        templates.ShouldContain("overmind://projects/{id}/planning/features/{slug}/current");
    }

    [Fact]
    public async Task PlanningBoardResource_RendersOneLinePerFeature()
    {
        var response = await Driver.ReadResourceAsync($"overmind://projects/{Canned.ProjectId}/planning/features");

        var text = Canned.ResourceText(response);
        text.ShouldContain("- demo-feature [inProgress] — Demo feature · tasks 1/2 · spec validated · plan draft");
        text.ShouldContain("- draft-one [draft] — Draft one · tasks 0/0 · spec none · plan none");

        FindRequest("GET", $"/api/agent/projects/{Canned.ProjectId}/planning/features");
    }

    [Fact]
    public async Task PlanningFeatureResource_RendersSectionsInOrder()
    {
        var response = await Driver.ReadResourceAsync(
            $"overmind://projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}");

        var text = Canned.ResourceText(response);
        text.ShouldStartWith("# Demo feature");
        text.ShouldContain("Ship the demo.");

        var spec = text.IndexOf("## Spec", StringComparison.Ordinal);
        var plan = text.IndexOf("## Plan", StringComparison.Ordinal);
        var tasks = text.IndexOf("## Tasks", StringComparison.Ordinal);
        var firstTask = text.IndexOf("### 01. Build the domain", StringComparison.Ordinal);
        var secondTask = text.IndexOf("### 02. Wire the API", StringComparison.Ordinal);
        spec.ShouldBeGreaterThan(0);
        plan.ShouldBeGreaterThan(spec);
        tasks.ShouldBeGreaterThan(plan);
        firstTask.ShouldBeGreaterThan(tasks);
        secondTask.ShouldBeGreaterThan(firstTask);

        text.ShouldContain("Status: done · Review: light");
        text.ShouldContain("Status: inProgress · Review: hard");
        text.ShouldNotContain("## Result");
    }

    [Fact]
    public async Task PlanningCurrentResource_TaskState_PrefixesTheTaskBody()
    {
        var response = await Driver.ReadResourceAsync(
            $"overmind://projects/{Canned.ProjectId}/planning/features/{Canned.PlanningSlug}/current");

        var text = Canned.ResourceText(response);
        text.ShouldStartWith("Current task: demo-feature #02 [inProgress]");
        text.ShouldContain("Wire the API");
        text.ShouldContain("Expose the endpoints.");
    }

    [Fact]
    public async Task PlanningCurrentResource_StopState_LeadsWithTheStopLine()
    {
        var response = await Driver.ReadResourceAsync(
            $"overmind://projects/{Canned.ProjectId}/planning/features/{Canned.PlanningStopSlug}/current");

        Canned.ResourceText(response).ShouldStartWith("STOP: spec awaits human validation");
    }

    [Fact]
    public async Task PlanningCurrentResource_EmptyState_LeadsWithNothingToDo()
    {
        var response = await Driver.ReadResourceAsync(
            $"overmind://projects/{Canned.ProjectId}/planning/features/{Canned.PlanningEmptySlug}/current");

        Canned.ResourceText(response).ShouldStartWith("NOTHING TO DO: All tasks are done");
    }

    [Fact]
    public async Task PlanningFeatureResource_UnknownFeature_MapsToResourceNotFound()
    {
        var response = await Driver.ReadResourceAsync(
            $"overmind://projects/{Canned.ProjectId}/planning/features/no-such-feature");

        var error = response["error"];
        error.ShouldNotBeNull("expected a JSON-RPC error, got: " + response.ToJsonString());
        error!["code"]!.GetValue<long>().ShouldBe(-32002);
    }
}
