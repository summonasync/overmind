using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningWorkflowApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningWorkflowApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_Defaults_ReturnsInheritUnset_WithEffectiveOn()
    {
        var project = await CreateProjectAsync();

        var workflow = await _client.GetFromJsonAsync<WorkflowJson>(WorkflowUrl(project.Id), JsonOptions);

        workflow.ShouldNotBeNull();
        workflow!.Materialization.ShouldBe("inherit");
        workflow.RequireHumanValidation.ShouldBe("unset");
        workflow.EffectiveMaterialization.ShouldBe("on");
        workflow.EffectiveRequireHumanValidation.ShouldBeFalse();
    }

    [Fact]
    public async Task Put_MaterializationOn_EffectiveOn()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync(WorkflowUrl(project.Id), new { Materialization = "on" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowJson>(JsonOptions);
        workflow.ShouldNotBeNull();
        workflow!.Materialization.ShouldBe("on");
        workflow.EffectiveMaterialization.ShouldBe("on");
        workflow.RequireHumanValidation.ShouldBe("unset");
    }

    [Fact]
    public async Task Put_ValidationOn_EffectiveTrue()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync(
            WorkflowUrl(project.Id), new { RequireHumanValidation = "on" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowJson>(JsonOptions);
        workflow.ShouldNotBeNull();
        workflow!.RequireHumanValidation.ShouldBe("on");
        workflow.EffectiveRequireHumanValidation.ShouldBeTrue();
        workflow.Materialization.ShouldBe("inherit");
    }

    [Fact]
    public async Task Put_ProjectOff_BeatsGlobalOn()
    {
        var project = await CreateProjectAsync();
        var settingsResponse = await _client.PutAsJsonAsync("/api/settings", new { PlanningMaterializationDefault = "on" });
        settingsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.PutAsJsonAsync(WorkflowUrl(project.Id), new { Materialization = "off" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowJson>(JsonOptions);
        workflow.ShouldNotBeNull();
        workflow!.EffectiveMaterialization.ShouldBe("off");
    }

    [Fact]
    public async Task Get_ProjectUnset_FollowsGlobalFlip()
    {
        var project = await CreateProjectAsync();
        var settingsResponse = await _client.PutAsJsonAsync("/api/settings", new { PlanningRequireHumanValidationDefault = "on" });
        settingsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var workflow = await _client.GetFromJsonAsync<WorkflowJson>(WorkflowUrl(project.Id), JsonOptions);

        workflow.ShouldNotBeNull();
        workflow!.RequireHumanValidation.ShouldBe("unset");
        workflow.EffectiveRequireHumanValidation.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync(WorkflowUrl(Guid.NewGuid()));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_UnknownProject_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            WorkflowUrl(Guid.NewGuid()), new { Materialization = "on" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_UnknownEnumStrings_Return400NamingAllowedValues()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PutAsJsonAsync(WorkflowUrl(project.Id), new
        {
            Materialization = "banana",
            RequireHumanValidation = "maybe",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem.ShouldNotBeNull();
        problem!.Errors.ShouldContainKey("Materialization");
        problem.Errors["Materialization"][0].ShouldContain("inherit");
        problem.Errors["Materialization"][0].ShouldContain("archive");

        var validationResponse = await _client.PutAsJsonAsync(
            WorkflowUrl(project.Id), new { RequireHumanValidation = "maybe" });
        validationResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var validationProblem = await validationResponse.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        validationProblem!.Errors.ShouldContainKey("RequireHumanValidation");
        validationProblem.Errors["RequireHumanValidation"][0].ShouldContain("unset");

        var unchanged = await _client.GetFromJsonAsync<WorkflowJson>(WorkflowUrl(project.Id), JsonOptions);
        unchanged!.Materialization.ShouldBe("inherit");
        unchanged.RequireHumanValidation.ShouldBe("unset");
    }

    private static string WorkflowUrl(Guid projectId) =>
        $"/api/projects/{projectId}/planning/workflow";

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"planning-workflow-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record WorkflowJson(
        string Materialization, string RequireHumanValidation,
        string EffectiveMaterialization, bool EffectiveRequireHumanValidation);

    public sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
