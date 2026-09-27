using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class TaskDependencyApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TaskDependencyApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Current_ChainResolution_WalksTheLowestActionableTask()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateChainAsync(project.Id, "feat");

        var first = await GetCurrentAsync(project.Id, "feat");
        first.State.ShouldBe("task");
        first.Artifact!.Order.ShouldBe(0);
        first.Artifact.BlockedBy.ShouldBeNull();

        await SetTaskStatusAsync(first.Artifact!.Id, "done");
        var second = await GetCurrentAsync(project.Id, "feat");
        second.Artifact!.Order.ShouldBe(1);
        second.Artifact.BlockedBy.ShouldBe([0]);

        await SetTaskStatusAsync(second.Artifact!.Id, "done");
        var third = await GetCurrentAsync(project.Id, "feat");
        third.Artifact!.Order.ShouldBe(2);
        third.Artifact.BlockedBy.ShouldBe([0, 1]);
    }

    [Fact]
    public async Task Current_AllCandidatesBlocked_StopNamesTheChain()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        var first = await CreateChainAsync(project.Id, "feat");
        await SetTaskStatusAsync(first.Id, "blocked");

        var current = await GetCurrentAsync(project.Id, "feat");

        current.State.ShouldBe("stop");
        current.Artifact.ShouldBeNull();
        current.Reason.ShouldBe(
            "STOP: every actionable task is blocked by unclosed tasks: " +
            "task 1 blocked by [0], task 2 blocked by [0, 1]. " +
            "Reads stay open; task issuance stops until every blocker closes (done or review).");
    }

    [Fact]
    public async Task Current_ReviewBlockerCountsAsClosed_StatusBlockedReblocks()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        var blocker = await CreateAgentTaskAsync(project.Id, "feat", "task zero");
        await CreateAgentTaskAsync(project.Id, "feat", "task one", blockedBy: new[] { 0 });
        await CreateAgentTaskAsync(project.Id, "feat", "task two");
        await SetTaskStatusAsync(blocker.Id, "review");

        var whileInReview = await GetCurrentAsync(project.Id, "feat");
        whileInReview.Artifact!.Order.ShouldBe(1);

        await SetTaskStatusAsync(blocker.Id, "blocked", "re-blocked after review findings");
        var afterReblock = await GetCurrentAsync(project.Id, "feat");
        afterReblock.Artifact!.Order.ShouldBe(2);
    }

    [Fact]
    public async Task Roster_BlockedFields_AreTaskOnlyAndNameSortedOrders()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentArtifactAsync(project.Id, new { Kind = "spec", Body = "# Spec" });
        await CreateChainAsync(project.Id, "feat");

        var raw = await _client.GetStringAsync(RosterUrl(project.Id, "feat"));
        var roster = JsonNode.Parse(raw)!["artifacts"]!.AsArray();

        var spec = roster.Single(i => i!["kind"]!.GetValue<string>() == "spec")!.AsObject();
        spec.ContainsKey("blocked").ShouldBeFalse();
        spec.ContainsKey("blockedBy").ShouldBeFalse();

        var tasks = roster.Where(i => i!["kind"]!.GetValue<string>() == "task")
            .Select(i => i!.AsObject())
            .ToDictionary(t => t["order"]!.GetValue<int>());
        tasks[0].ContainsKey("blocked").ShouldBeFalse();
        tasks[0].ContainsKey("blockedBy").ShouldBeFalse();
        tasks[1]["blocked"]!.GetValue<bool>().ShouldBeTrue();
        tasks[1]["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0]);
        tasks[2]["blocked"]!.GetValue<bool>().ShouldBeTrue();
        tasks[2]["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task FullRead_EdgelessFeature_OmitsBlockedFieldsEntirely()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentTaskAsync(project.Id, "feat", "task zero");
        await CreateAgentTaskAsync(project.Id, "feat", "task one");

        var feature = JsonNode.Parse(
            await _client.GetStringAsync($"/api/agent/projects/{project.Id}/planning/features/feat"))!;
        feature["artifacts"]!.AsArray().ShouldAllBe(a =>
            !a!.AsObject().ContainsKey("blocked") && !a.AsObject().ContainsKey("blockedBy"));

        var rosterRaw = await _client.GetStringAsync(RosterUrl(project.Id, "feat"));
        rosterRaw.ShouldNotContain("blockedBy");
        rosterRaw.ShouldNotContain("\"blocked\"");

        var currentRaw = await _client.GetStringAsync(CurrentUrl(project.Id, "feat"));
        currentRaw.ShouldNotContain("blockedBy");
        currentRaw.ShouldNotContain("\"blocked\"");
        JsonNode.Parse(currentRaw)!["state"]!.GetValue<string>().ShouldBe("task");
    }

    [Fact]
    public async Task Batch_WithBlockedBy_CreatesEdgesAndEchoesThem()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            TasksUrl(project.Id, "feat"),
            new
            {
                Tasks = new object[]
                {
                    new { Body = "task zero" },
                    new { Body = "task one", BlockedBy = new[] { 0 } },
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        var batch = JsonNode.Parse(raw)!;
        batch["created"]!.GetValue<int>().ShouldBe(2);
        var echoed = batch["artifacts"]!.AsArray()
            .Single(a => a!["order"]!.GetValue<int>() == 1)!.AsObject();
        echoed["blocked"]!.GetValue<bool>().ShouldBeTrue();
        echoed["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0]);

        var roster = JsonNode.Parse(await _client.GetStringAsync(RosterUrl(project.Id, "feat")))!;
        var edged = roster["artifacts"]!.AsArray()
            .Single(a => a!["order"]!.GetValue<int>() == 1)!.AsObject();
        edged["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0]);
    }

    [Fact]
    public async Task Batch_UnknownBlockerOrder_Returns400NamingTheItemAndWritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            TasksUrl(project.Id, "feat"),
            new
            {
                Tasks = new object[]
                {
                    new { Body = "task zero", BlockedBy = new[] { 1 } },
                    new { Body = "task one", BlockedBy = new[] { 7 } },
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("tasks[1]");
        raw.ShouldContain("blockedBy order 7");

        var feature = await _client.GetFromJsonAsync<FeaturePayload>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Artifacts.Length.ShouldBe(0);
        feature.Status.ShouldBe("draft");
    }

    [Fact]
    public async Task Batch_CycleAcrossItems_Returns400NamingThePath()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            TasksUrl(project.Id, "feat"),
            new
            {
                Tasks = new object[]
                {
                    new { Body = "task zero" },
                    new { Body = "task one", BlockedBy = new[] { 0 } },
                    new { Body = "task two", Order = 0, BlockedBy = new[] { 1 } },
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("tasks[2]");
        raw.ShouldContain("dependency cycle");
    }

    [Fact]
    public async Task SingleUpsert_WithBlockedBy_CreatesEdges()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentArtifactAsync(project.Id, new { Kind = "task", Body = "task zero" });
        var edged = await CreateAgentArtifactAsync(project.Id,
            new { Kind = "task", Body = "task one", Order = 1, BlockedBy = new[] { 0 } });

        edged.Order.ShouldBe(1);
        var roster = JsonNode.Parse(await _client.GetStringAsync(RosterUrl(project.Id, "feat")))!;
        var one = roster["artifacts"]!.AsArray()
            .Single(a => a!["order"]!.GetValue<int>() == 1)!.AsObject();
        one["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0]);
    }

    [Fact]
    public async Task SingleUpsert_EmptyBlockedBy_ClearsEdges()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentArtifactAsync(project.Id, new { Kind = "task", Body = "task zero" });
        await CreateAgentArtifactAsync(project.Id,
            new { Kind = "task", Body = "task one", Order = 1, BlockedBy = new[] { 0 } });

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task one", Order = 1, BlockedBy = Array.Empty<int>() });

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var roster = JsonNode.Parse(await _client.GetStringAsync(RosterUrl(project.Id, "feat")))!;
        var one = roster["artifacts"]!.AsArray()
            .Single(a => a!["order"]!.GetValue<int>() == 1)!.AsObject();
        one.ContainsKey("blockedBy").ShouldBeFalse();
    }

    [Fact]
    public async Task SingleUpsert_AbsentBlockedBy_PreservesEdges()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentArtifactAsync(project.Id, new { Kind = "task", Body = "task zero" });
        await CreateAgentArtifactAsync(project.Id,
            new { Kind = "task", Body = "task one", Order = 1, BlockedBy = new[] { 0 } });

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task one", Order = 1 });

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var roster = JsonNode.Parse(await _client.GetStringAsync(RosterUrl(project.Id, "feat")))!;
        var one = roster["artifacts"]!.AsArray()
            .Single(a => a!["order"]!.GetValue<int>() == 1)!.AsObject();
        one["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0]);
    }

    [Fact]
    public async Task SingleUpsert_UnknownBlockerOrder_Returns400NamingTheOrder()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentArtifactAsync(project.Id, new { Kind = "task", Body = "task zero" });

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task one", Order = 1, BlockedBy = new[] { 7 } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("blockedBy order 7");

        var feature = await _client.GetFromJsonAsync<FeaturePayload>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Artifacts.Length.ShouldBe(1);
    }

    [Fact]
    public async Task SingleUpsert_NegativeBlockerOrder_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/artifacts",
            new { Kind = "task", Body = "task zero", BlockedBy = new[] { -1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var feature = await _client.GetFromJsonAsync<FeaturePayload>(
            $"/api/agent/projects/{project.Id}/planning/features/feat", JsonOptions);
        feature!.Artifacts.Length.ShouldBe(0);
    }

    [Fact]
    public async Task BatchAndSingleDoors_ProduceTheSameEdges()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        await CreateAgentTaskAsync(project.Id, "feat", "task zero");
        await CreateAgentArtifactAsync(project.Id,
            new { Kind = "task", Body = "task one", Order = 1, BlockedBy = new[] { 0 } });
        await CreateAgentTaskAsync(project.Id, "feat", "task two", blockedBy: new[] { 0, 1 });

        var roster = JsonNode.Parse(await _client.GetStringAsync(RosterUrl(project.Id, "feat")))!;
        var tasks = roster["artifacts"]!.AsArray()
            .Where(a => a!["kind"]!.GetValue<string>() == "task")
            .Select(a => a!.AsObject())
            .ToDictionary(t => t["order"]!.GetValue<int>());
        tasks[1]["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0]);
        tasks[2]["blockedBy"]!.AsArray().Select(b => b!.GetValue<int>()).ShouldBe([0, 1]);
    }

    private async Task<ArtifactPayload> CreateChainAsync(Guid projectId, string slug)
    {
        var zero = await CreateAgentTaskAsync(projectId, slug, "task zero");
        await CreateAgentTaskAsync(projectId, slug, "task one", blockedBy: new[] { 0 });
        await CreateAgentTaskAsync(projectId, slug, "task two", blockedBy: new[] { 0, 1 });
        return zero;
    }

    private async Task<ArtifactPayload> CreateAgentTaskAsync(
        Guid projectId, string slug, string body, int[]? blockedBy = null)
    {
        var response = await _client.PostAsJsonAsync(
            TasksUrl(projectId, slug),
            new { Tasks = new[] { new { Body = body, BlockedBy = blockedBy } } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var batch = await response.Content.ReadFromJsonAsync<TaskBatchPayload>(JsonOptions);
        return batch!.Artifacts.Single();
    }

    private async Task<ArtifactPayload> CreateAgentArtifactAsync(Guid projectId, object body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/feat/artifacts", body);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ArtifactPayload>(JsonOptions))!;
    }

    private async Task<CurrentPayload> GetCurrentAsync(Guid projectId, string slug) =>
        (await _client.GetFromJsonAsync<CurrentPayload>(CurrentUrl(projectId, slug), JsonOptions))!;

    private async Task SetTaskStatusAsync(Guid artifactId, string status, string? logNote = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/planning/artifacts/{artifactId}/status",
            new { Status = status, LogNote = logNote ?? $"marking {status}" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<ProjectPayload> CreateProjectAsync()
    {
        var name = $"task-dep-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectPayload>(JsonOptions))!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = "Task dependency feature", Body = "" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static string TasksUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts/tasks";

    private static string RosterUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private static string CurrentUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/current";

    private sealed record ProjectPayload(Guid Id, string Name);

    private sealed record FeaturePayload(string Slug, string Status, ArtifactPayload[] Artifacts);

    private sealed record ArtifactPayload(
        Guid Id, string Kind, string Status, int Order, int[]? BlockedBy = null);

    private sealed record TaskBatchPayload(int Created, int Updated, ArtifactPayload[] Artifacts);

    private sealed record CurrentPayload(
        string State, CurrentFeaturePayload Feature, ArtifactPayload? Artifact, string? Reason);

    private sealed record CurrentFeaturePayload(string Slug, string Title, string Status);
}
