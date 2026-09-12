using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentPlanningApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentPlanningApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Upsert_SpecFirstCallCreatesSecondCallVersions()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var first = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v1" });
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await first.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        created!.Version.ShouldBe(1);

        var second = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v2" });
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await second.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Version.ShouldBe(2);

        var versions = await _client.GetFromJsonAsync<VersionJson[]>(
            $"/api/planning/artifacts/{created.Id}/versions", JsonOptions);
        versions.ShouldNotBeNull();
        versions!.Length.ShouldBe(1);
        versions[0].Version.ShouldBe(1);
        versions[0].Body.ShouldBe("v1");
    }

    [Fact]
    public async Task Upsert_FrozenSpecWithoutLogNote_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v2" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("ChangeNoteRequired");
    }

    [Fact]
    public async Task Upsert_FrozenSpecWithLogNote_Returns200AndResetsToDraft()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "v1" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var response = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "spec", Body = "v2", LogNote = "unfreezing" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Status.ShouldBe("draft");
        updated.FrozenAt.ShouldBeNull();
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_TaskByExplicitOrder_CreatesDistinctAndUpdatesInPlace()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task A", Order = 5 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task B", Order = 7 });
        first.Id.ShouldNotBe(second.Id);

        var updateResponse = await _client.PostAsJsonAsync(
            AgentArtifactsUrl(project.Id, "feat"), new { Kind = "task", Body = "task A v2", Order = 5 });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        updated!.Id.ShouldBe(first.Id);
        updated.Version.ShouldBe(2);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        var tasks = feature!.Artifacts.Where(a => a.Kind == "task").ToArray();
        tasks.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Validate_GateOn_Returns409HumanValidationRequired_WritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("HumanValidationRequired");
        problem.Detail.ShouldBe(
            "STOP: the spec of feature 'feat' awaits human validation — ask the user to approve it in the Overmind UI.");

        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Status.ShouldBe("draft");
        artifact.FrozenAt.ShouldBeNull();
    }

    [Fact]
    public async Task Validate_ProjectOffBeatsGlobalOn_Returns200Validated()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var settings = await _client.PutAsJsonAsync(
            "/api/settings", new { PlanningRequireHumanValidationDefault = "on" });
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        await SetValidationGateAsync(project.Id, on: false);

        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var response = await _client.PostAsync(AgentValidateUrl(spec.Id), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(
            $"/api/planning/artifacts/{spec.Id}", JsonOptions);
        artifact!.Status.ShouldBe("validated");
        artifact.FrozenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Current_GateOn_NoSpec_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("STOP: spec awaits human validation — it cannot be approved through MCP.");
        current.Feature.Slug.ShouldBe("feat");
    }

    [Fact]
    public async Task Current_GateOn_SpecDraft_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Reason.ShouldBe("STOP: spec awaits human validation — it cannot be approved through MCP.");
    }

    [Fact]
    public async Task Current_GateOn_SpecValidatedPlanMissing_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("STOP: plan awaits human validation — it cannot be approved through MCP.");
    }

    [Fact]
    public async Task Current_GateOn_PlanDraft_ReturnsStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "# Plan" });

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Reason.ShouldBe("STOP: plan awaits human validation — it cannot be approved through MCP.");
    }

    [Fact]
    public async Task Current_GateOn_SpecAndPlanValidatedNoTasks_ReturnsEmpty()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await SetValidationGateAsync(project.Id, on: true);
        var spec = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await FreezeViaHumanDoorAsync(spec.Id);
        var plan = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "plan", Body = "# Plan" });
        await FreezeViaHumanDoorAsync(plan.Id);

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("No tasks yet — create them from the validated plan.");
    }

    [Fact]
    public async Task Current_GateOff_SkipsSpecPlanStop()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Reason.ShouldBe("No tasks yet — create them from the validated plan.");
    }

    [Fact]
    public async Task Current_AllTasksDone_ReturnsEmpty()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "done");
        await SetTaskStatusAsync(second.Id, "done");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("empty");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe("All tasks are done — write the result artifact.");
    }

    [Fact]
    public async Task Current_ReturnsFirstOpenTaskByOrder()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(first.Id, "done");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Reason.ShouldBeNull();
        current.Artifact.ShouldNotBeNull();
        current.Artifact!.Order.ShouldBe(2);
        current.Artifact.Status.ShouldBe("todo");
    }

    [Fact]
    public async Task Current_SkipsBlockedTask_ForLaterActionable()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var blocked = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        await SetTaskStatusAsync(blocked.Id, "blocked");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Reason.ShouldBeNull();
        current.Artifact!.Order.ShouldBe(2);
    }

    [Fact]
    public async Task Current_AllRemainingBlocked_ReturnsBlockedWithReason()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        var first = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1", Order = 1 });
        var second = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 2", Order = 2 });
        var third = await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 3", Order = 3 });
        await SetTaskStatusAsync(first.Id, "done");
        await SetTaskStatusAsync(second.Id, "blocked");
        await SetTaskStatusAsync(third.Id, "blocked");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("task");
        current.Artifact!.Order.ShouldBe(2);
        current.Reason.ShouldBe("The next task is blocked.");
    }

    [Fact]
    public async Task BoardAndFeature_SameJsonShapeAsHumanDoor()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "feat", "Feature");
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "spec", Body = "# Spec" });
        await CreateAgentArtifactAsync(project.Id, "feat", new { Kind = "task", Body = "task 1" });

        var agentBoard = await GetRawAsync($"/api/agent/projects/{project.Id}/planning/features");
        var humanBoard = await GetRawAsync($"/api/projects/{project.Id}/planning/features");
        PropertyNames(agentBoard).ShouldBe(PropertyNames(humanBoard));

        var agentFeature = await GetRawAsync($"/api/agent/projects/{project.Id}/planning/features/feat");
        var humanFeature = await GetRawAsync($"/api/projects/{project.Id}/planning/features/feat");
        PropertyNames(agentFeature).ShouldBe(PropertyNames(humanFeature));

        var agentParsed = JsonDocument.Parse(agentFeature).RootElement;
        agentParsed.GetProperty("slug").GetString().ShouldBe("feat");
        agentParsed.GetProperty("artifacts").GetArrayLength().ShouldBe(2);
    }

    private static string AgentArtifactsUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private static string AgentValidateUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/validate";

    private async Task<CurrentJson> GetCurrentAsync(Guid projectId, string slug)
    {
        var response = await _client.GetAsync($"/api/agent/projects/{projectId}/planning/features/{slug}/current");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = await response.Content.ReadFromJsonAsync<CurrentJson>(JsonOptions);
        current.ShouldNotBeNull();
        return current!;
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"agent-planning-{Guid.NewGuid():N}";
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

    private async Task<FeatureJson> CreateAgentFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var feature = await response.Content.ReadFromJsonAsync<FeatureJson>(JsonOptions);
        feature.ShouldNotBeNull();
        return feature!;
    }

    private async Task<ArtifactJson> CreateAgentArtifactAsync(Guid projectId, string slug, object body)
    {
        var response = await _client.PostAsJsonAsync(AgentArtifactsUrl(projectId, slug), body);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        return artifact!;
    }

    private async Task SetValidationGateAsync(Guid projectId, bool on)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/workflow",
            new { RequireHumanValidation = on ? "on" : "off" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SetTaskStatusAsync(Guid artifactId, string status)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/planning/artifacts/{artifactId}/status",
            new { Status = status, LogNote = $"marking {status}" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task FreezeViaHumanDoorAsync(Guid artifactId)
    {
        var response = await _client.PostAsync($"/api/planning/artifacts/{artifactId}/validate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string> GetRawAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    private static List<string> PropertyNames(string json)
    {
        var names = new List<string>();
        Walk(JsonDocument.Parse(json).RootElement, names);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static void Walk(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Walk(property.Value, names);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, names);
                }
                break;
        }
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record FeatureJson(
        string Slug, string Title, string Body, string Status,
        int Version, DateTime CreatedAt, DateTime UpdatedAt, ArtifactJson[] Artifacts);

    public sealed record ArtifactJson(
        Guid Id, string Kind, string Status, string Review,
        int Order, string Body, DateTime? FrozenAt, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, LogLineJson[] Log);

    public sealed record LogLineJson(string? Date, string Text);

    public sealed record VersionJson(int Version, string Body, DateTime CreatedAt);

    public sealed record CurrentJson(
        string State, CurrentFeatureJson Feature, ArtifactJson? Artifact, string? Reason);

    public sealed record CurrentFeatureJson(string Slug, string Title, string Status);

    public sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
