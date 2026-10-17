using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class WaveSnapshotApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WaveSnapshotApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-wave-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_sandboxDir, "project");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Snapshot_AggregatesQueueBlockersAndEntries()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "wave-a", "Wave A");
        await CreateFeature(project.Id, "wave-b", "Wave B");
        await BringToStatusAsync(project.Id, "wave-a", "inProgress");
        await AddDependencyAsync(project.Id, "wave-b", "wave-a");
        await CreateEntryAsync(project.Id, "wave standup", "lane-a moving", ["wave-sync"]);
        await CreateEntryAsync(project.Id, "unrelated", "no tags", ["release"]);

        var snapshot = await GetSnapshotAsync(project.Id);

        snapshot.ShouldNotBeNull();
        snapshot.CoordinationTags.ShouldBe(["wave-sync", "lane-marshalling"]);
        snapshot.Queue.Select(i => i.Slug).ShouldBe(["wave-a", "wave-b"]);
        snapshot.CodeLocations.Select(c => c.Slug).ShouldBe(["wave-a"]);
        snapshot.OpenBlockers.ShouldHaveSingleItem();
        snapshot.OpenBlockers.Single().Slug.ShouldBe("wave-b");
        snapshot.OpenBlockers.Single().OpenBlockers.ShouldBe(["wave-a"]);
        snapshot.Claims.ShouldBeEmpty();
        snapshot.Entries.Select(e => e.Title).ShouldBe(["wave standup"]);
        snapshot.Entries.Single().Tags.ShouldBe(["wave-sync"]);
        snapshot.Entries.Single().Snippet.ShouldContain("lane-a moving");
    }

    [Fact]
    public async Task Snapshot_ClaimedFeatureSurfacesClaimIdentity()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "claimed", "Claimed");
        using var claimRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimed/claim")
        {
            Content = JsonContent.Create(new { Alias = "marshal", Vendor = "claude" })
        };
        claimRequest.Headers.Add(SessionHeaderName, Guid.NewGuid().ToString());
        var claim = await _client.SendAsync(claimRequest);
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);

        var snapshot = await GetSnapshotAsync(project.Id);

        var row = snapshot!.Claims.ShouldHaveSingleItem();
        row.Slug.ShouldBe("claimed");
        row.ClaimedByAlias.ShouldBe("marshal");
        row.ClaimedByVendor.ShouldBe("claude");
        row.ClaimState.ShouldBeOneOf("alive", "stale", "ended");
        row.LastActivityAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Snapshot_TagQueryOverridesTheDefaults()
    {
        var project = await CreateProjectAsync();
        await CreateEntryAsync(project.Id, "default", "body", ["wave-sync"]);
        await CreateEntryAsync(project.Id, "custom", "body", ["custom-lane"]);

        var response = await _client.GetAsync(
            $"/api/agent/projects/{project.Id}/planning/wave-snapshot?tags=custom-lane");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshot = await ReadSnapshotAsync(response);
        snapshot!.CoordinationTags.ShouldBe(["custom-lane"]);
        snapshot.Entries.Select(e => e.Title).ShouldBe(["custom"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Snapshot_NonPositiveEntryLimit_Returns400(int entryLimit)
    {
        var project = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/agent/projects/{project.Id}/planning/wave-snapshot?entryLimit={entryLimit}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.ShouldContainKey("entryLimit");
    }

    [Fact]
    public async Task Snapshot_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync($"/api/agent/projects/{Guid.NewGuid()}/planning/wave-snapshot");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Snapshot_IsAgentDoorOnly()
    {
        var project = await CreateProjectAsync();

        var humanDoor = await _client.GetAsync($"/api/projects/{project.Id}/planning/wave-snapshot");

        humanDoor.StatusCode.ShouldBe(HttpStatusCode.OK);
        humanDoor.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
    }

    private async Task<WaveSnapshotJson?> GetSnapshotAsync(Guid projectId)
    {
        var response = await _client.GetAsync($"/api/agent/projects/{projectId}/planning/wave-snapshot");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadSnapshotAsync(response);
    }

    private static async Task<WaveSnapshotJson?> ReadSnapshotAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<WaveSnapshotJson>(JsonOptions);

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"wave-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateFeature(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task CreateEntryAsync(Guid projectId, string title, string content, string[] tags)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "note",
            Title = title,
            Content = content,
            Tags = tags,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task AddDependencyAsync(Guid projectId, string slug, string blockedBy)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/dependencies",
            new { BlockedBy = blockedBy });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task BringToStatusAsync(Guid projectId, string slug, string target)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Status = target });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string SessionHeaderName => "X-Overmind-Session";

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record WaveSnapshotJson(
        Guid ProjectId,
        DateTime GeneratedAt,
        string[] CoordinationTags,
        QueueItemJson[] Queue,
        ClaimJson[] Claims,
        CodeLocationJson[] CodeLocations,
        BlockersJson[] OpenBlockers,
        EntryJson[] Entries);

    public sealed record QueueItemJson(
        string Slug, string? Number, string Title, string Priority, string Status, int QueuePosition,
        string? SpecStatus, string? PlanStatus, int TaskDone, int TaskTotal,
        TaskCountsJson TaskCounts, int Readiness, string[] BlockedBy, bool IsBlocked);

    public sealed record ClaimJson(
        string Slug,
        string ClaimedByAlias,
        string ClaimedByVendor,
        DateTime ClaimedAt,
        string ClaimState,
        bool ClaimedByHuman,
        DateTime LastActivityAt,
        int QueuePosition,
        string Priority,
        string Status);

    public sealed record CodeLocationJson(string Slug, string? PinnedBranch, string? PinnedWorktreePath);

    public sealed record BlockersJson(string Slug, string[] OpenBlockers);

    public sealed record EntryJson(Guid Id, string Type, string Title, string[] Tags, string Snippet, DateTime UpdatedAt);

    public sealed record TaskCountsJson(int Todo, int InProgress, int Review, int Done, int Blocked);

    public sealed record ValidationProblemJson([property: JsonPropertyName("errors")] Dictionary<string, string[]> Errors);
}
