using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningDryRunApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningDryRunApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task UpsertArtifact_DryRunCreate_EchoesWouldBeAndPersistsNothing()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = "# Spec\n\n<goal>Ship it.</goal>", DryRun = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var staged = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        staged!.DryRun.ShouldBe(true);
        staged.Version.ShouldBe(1);
        staged.Id.ShouldNotBe(Guid.Empty);

        var roster = await _client.GetStringAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts");
        roster.ShouldContain("\"artifacts\":[]");
    }

    [Fact]
    public async Task UpsertArtifact_DryRunUpdate_EchoesWouldBeVersionAndPersistsNothing()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");
        var created = await CreateArtifactAsync(
            project.Id, "feat", new { Kind = "spec", Body = "v1" });

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = "v2", LogNote = "dry edit", DryRun = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var staged = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        staged!.DryRun.ShouldBe(true);
        staged.Id.ShouldBe(created.Id);
        staged.Version.ShouldBe(2);

        var stored = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/agent/planning/artifacts/{created.Id}", JsonOptions);
        stored!.Version.ShouldBe(1);
        stored.Body.ShouldBe("v1");

        var versions = await _client.GetStringAsync($"/api/planning/artifacts/{created.Id}/versions");
        versions.ShouldNotContain("v2");
    }

    [Fact]
    public async Task UpsertArtifact_DryRunFalse_StillPersists()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = "# Spec", DryRun = false });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        created!.DryRun.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertArtifact_MalformedPointerLine_400CarriesAcceptedFormAndExample()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"),
            new { Kind = "spec", Body = "# Spec\n\npointer: src/Auth/Keys.cs (see below)\n" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail.ShouldContain("Malformed pointer line");
        problem.Detail.ShouldContain("'pointer: <repo-relative-path>[#anchor]'");
        problem.Detail.ShouldContain("Example:");
    }

    [Fact]
    public async Task UpsertTasks_DryRun_EchoesWouldBeOrdersAndPersistsNothing()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new
            {
                DryRun = true,
                Tasks = new object[]
                {
                    new { Body = "task zero" },
                    new { Body = "task one", Order = 5 }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var batch = await response.Content.ReadFromJsonAsync<TaskBatchJson>(JsonOptions);
        batch!.DryRun.ShouldBe(true);
        batch.Created.ShouldBe(2);
        batch.Artifacts.Select(a => a.Order).ShouldBe([0, 5]);

        var roster = await _client.GetStringAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts");
        roster.ShouldContain("\"artifacts\":[]");
    }

    [Fact]
    public async Task UpsertTasks_MissingBody_Envelope400CarriesAcceptedForm()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new { Tasks = new object[] { new { Order = 0 } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("Body is required on every task item");
        raw.ShouldContain("<goal>Fix the login redirect.</goal>");
    }

    [Fact]
    public async Task UpsertTasks_EmptyBatch_Envelope400NamesTheTasksArrayForm()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "Feature");

        var response = await _client.PostAsJsonAsync(
            AgentTasksUrl(project.Id, "feat"),
            new { Tasks = Array.Empty<object>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("A task batch needs at least one item");
        raw.ShouldContain("\\\"tasks\\\": [ { \\\"body\\\"");
    }

    private static string AgentArtifactsUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private static string AgentTasksUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts/tasks";

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"dry-run-{Guid.NewGuid():N}";
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

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<ArtifactJson> CreateArtifactAsync(Guid projectId, string slug, object body)
    {
        var response = await _client.PostAsJsonAsync(AgentArtifactsUrl(projectId, slug), body);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        return artifact!;
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record ArtifactJson(
        Guid Id, string Kind, string Status, int Order, string Body, int Version, bool? DryRun = null);

    public sealed record TaskBatchJson(int Created, int Updated, ArtifactJson[] Artifacts, bool? DryRun = null);

    public sealed record ProblemJson(string Title, string Detail);
}
