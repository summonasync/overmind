using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class HumanInboxApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HumanInboxApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-inbox-{Guid.NewGuid():N}");
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
    public async Task Inbox_AwaitingHumanFeature_ReturnsEntryWithPendingSince()
    {
        var project = await CreateProjectAsync();
        await SeedAwaitingHumanAsync(project.Id, "wait-tap");

        var inbox = await GetInboxAsync(project.Id);

        inbox!.Items.Length.ShouldBe(1);
        var entry = inbox.Items.Single();
        entry.Slug.ShouldBe("wait-tap");
        entry.Title.ShouldBe("Wait tap");
        entry.Phase.ShouldBe("awaiting-human");
        entry.QueuePosition.ShouldBe(1);
        entry.ClaimedByAlias.ShouldBeNull();
        entry.ClaimState.ShouldBeNull();
        entry.PendingSince.ShouldNotBe(default);
        inbox.ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task Inbox_WithBoundDashboardOrigin_CarriesTheDeepLinkTarget()
    {
        _factory.Services.GetRequiredService<BrowserOrigin>()
            .Bind(new Uri("https://dash.example"));
        var project = await CreateProjectAsync();
        await SeedAwaitingHumanAsync(project.Id, "wait-tap");

        var inbox = await GetInboxAsync(project.Id);

        inbox!.Items.Single().Link.ShouldBe(
            $"https://dash.example/dashboard?project={project.Id}&feature=wait-tap&tab=result");
    }

    [Fact]
    public async Task Inbox_ClaimedFeature_ShowsHolderAlias()
    {
        var project = await CreateProjectAsync();
        await SeedAwaitingHumanAsync(project.Id, "wait-tap");

        var claim = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/wait-tap/claim",
            new { Alias = "board", Vendor = "human" });
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);

        var inbox = await GetInboxAsync(project.Id);

        inbox!.Items.Single().ClaimedByAlias.ShouldBe("board");
        inbox.Items.Single().ClaimState.ShouldBe("alive");
    }

    [Fact]
    public async Task Inbox_Unclaim_KeepsTheFeaturePhase()
    {
        var project = await CreateProjectAsync();
        await SeedAwaitingHumanAsync(project.Id, "wait-tap");
        await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/wait-tap/claim",
            new { Alias = "board", Vendor = "human" });

        var unclaim = await _client.PostAsync(
            $"/api/projects/{project.Id}/planning/features/wait-tap/unclaim", null);
        unclaim.StatusCode.ShouldBe(HttpStatusCode.OK);

        var inbox = await GetInboxAsync(project.Id);

        inbox!.Items.Single().Phase.ShouldBe("awaiting-human");
        inbox.Items.Single().ClaimedByAlias.ShouldBeNull();
    }

    [Fact]
    public async Task Inbox_EmptyProject_ReturnsEmptyList()
    {
        var project = await CreateProjectAsync();

        var inbox = await GetInboxAsync(project.Id);

        inbox.ShouldNotBeNull();
        inbox!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Inbox_OtherProjectsFeatures_AreAbsent()
    {
        var project = await CreateProjectAsync();
        var other = await CreateProjectAsync();
        await SeedAwaitingHumanAsync(project.Id, "mine-tap");
        await SeedAwaitingHumanAsync(other.Id, "theirs-tap");

        var inbox = await GetInboxAsync(project.Id);
        var otherInbox = await GetInboxAsync(other.Id);

        inbox!.Items.Select(i => i.Slug).ShouldBe(["mine-tap"]);
        otherInbox!.Items.Select(i => i.Slug).ShouldBe(["theirs-tap"]);
    }

    [Fact]
    public async Task Inbox_FeatureWithoutValidatedResult_IsAbsent()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "draft-close", "Draft close");

        var inbox = await GetInboxAsync(project.Id);

        inbox!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Inbox_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync($"/api/human/inbox?projectId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HumanDigest_FlagDefaultOff_AndFlips()
    {
        var project = await CreateProjectAsync();

        var enabled = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/human-digest", new { Enabled = true });
        enabled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterEnable = await enabled.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        afterEnable!.HumanDigestEnabled.ShouldBeTrue();

        var disabled = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/human-digest", new { Enabled = false });
        var afterDisable = await disabled.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        afterDisable!.HumanDigestEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task HumanDigest_UnknownProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.NewGuid()}/human-digest", new { Enabled = true });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<InboxJson?> GetInboxAsync(Guid projectId)
    {
        var response = await _client.GetAsync($"/api/human/inbox?projectId={projectId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<InboxJson>(JsonOptions);
    }

    private async Task SeedAwaitingHumanAsync(Guid projectId, string slug)
    {
        await CreateFeature(projectId, slug, "Wait tap");
        var specId = await CreateArtifactAsync(projectId, slug, "spec");
        await ValidateArtifactAsync(specId);
        var planId = await CreateArtifactAsync(projectId, slug, "plan");
        await ValidateArtifactAsync(planId);
        foreach (var taskId in await CreateTasksAsync(projectId, slug, 2))
        {
            await CompleteTaskAsync(taskId);
        }

        var resultId = await CreateArtifactAsync(projectId, slug, "result");
        await ValidateArtifactAsync(resultId);
    }

    private async Task<Guid[]> CreateTasksAsync(Guid projectId, string slug, int count)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(await CreateArtifactAsync(projectId, slug, "task"));
        }

        return [.. ids];
    }

    private async Task CompleteTaskAsync(Guid taskId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/planning/artifacts/{taskId}/status",
            new { Status = "done", LogNote = "task closed done" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"inbox-proj-{Guid.NewGuid():N}",
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

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, string kind)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/artifacts",
            new { Kind = kind, Body = $"# {kind}" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        return artifact!.Id;
    }

    private async Task ValidateArtifactAsync(Guid artifactId)
    {
        var response = await _client.PostAsync($"/api/planning/artifacts/{artifactId}/validate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public sealed record ProjectJson(
        Guid Id, string Name, string DiskPath, string Description, string? Manifest,
        string NumberStyle, string? NumberPrefix, bool IsFavorite, bool AutoActOnReplies,
        bool HumanDigestEnabled, int OpenFeatureCount, DateTime CreatedAt);

    public sealed record InboxJson(Guid ProjectId, InboxItemJson[] Items);

    public sealed record InboxItemJson(
        string Slug, string Title, string Phase, int QueuePosition,
        string? ClaimedByAlias, string? ClaimState, DateTime PendingSince, string? Link);

    public sealed record ArtifactJson(Guid Id);
}
