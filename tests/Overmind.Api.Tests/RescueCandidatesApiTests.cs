using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class RescueCandidatesApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;
    private readonly string _databasePath;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RescueCandidatesApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-rescue-{Guid.NewGuid():N}.db");
        _databasePath = databasePath;
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = _databasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task Door_ListsStaleAndEndedClaimsClaimLockFree()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "f-alive");
        await CreateFeatureAsync(project.Id, "f-stale");
        await CreateFeatureAsync(project.Id, "f-ended");
        await CreateFeatureAsync(project.Id, "f-unclaimed");
        await CreateFeatureAsync(project.Id, "f-draft");
        await BringToStatusAsync(project.Id, "f-alive", "inProgress");
        await BringToStatusAsync(project.Id, "f-stale", "inProgress");
        await BringToStatusAsync(project.Id, "f-stale", "review");
        await BringToStatusAsync(project.Id, "f-ended", "inProgress");

        var aliveSeen = DateTime.UtcNow.AddMinutes(-1);
        var staleSeen = DateTime.UtcNow.AddHours(-2);
        await SeedSessionAsync("alive-s", "dan", "qwen", aliveSeen);
        await SeedSessionAsync("stale-s", "dan", "qwen", staleSeen);
        await SeedSessionAsync("ended-s", "dan", "qwen", staleSeen, staleSeen);
        await SeedClaimAsync(project.Id, "f-alive", "dan", "qwen", "alive-s", staleSeen);
        await SeedClaimAsync(project.Id, "f-stale", "column-dan", "qwen", "stale-s", staleSeen);
        await SeedClaimAsync(project.Id, "f-ended", "dan", "qwen", "ended-s", staleSeen);
        await SeedClaimAsync(project.Id, "f-draft", "dan", "qwen", "stale-s", staleSeen);
        await SeedSpanAsync(project.Id, "f-stale", "ledger-dan", "claude", "stale-s",
            DateTime.UtcNow.AddHours(-6), null);
        await SeedSpanAsync(project.Id, "f-alive", "dan", "qwen", "alive-s",
            DateTime.UtcNow.AddHours(-3), null);

        var response = await _client.GetAsync(
            $"/api/agent/projects/{project.Id}/planning/rescue-candidates");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadCandidatesAsync(response);
        body!.ProjectId.ShouldBe(project.Id);
        body.Candidates.Select(c => c.Slug).ShouldBe(["f-stale", "f-ended"]);

        var stale = body.Candidates[0];
        stale.Status.ShouldBe("review");
        stale.ClaimState.ShouldBe("stale");
        stale.ClaimedByAlias.ShouldBe("ledger-dan");
        stale.ClaimedByVendor.ShouldBe("claude");
        stale.ClaimedAt.ShouldBeLessThan(DateTime.UtcNow.AddHours(-5));
        stale.UpdatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Door_CarriesCodePinFromTheFeature()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "pinned");
        await BringToStatusAsync(project.Id, "pinned", "inProgress");
        await SeedSessionAsync("gone-s", "dan", "qwen", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));
        await SeedClaimAsync(project.Id, "pinned", "dan", "qwen", "gone-s", DateTime.UtcNow.AddHours(-2));
        await ExecuteAsync($"""
            UPDATE PlanningFeatures SET PinnedBranch = 'feat/235-orphan', PinnedWorktreePath = '/lanes/235'
            WHERE ProjectId = '{project.Id.ToString().ToUpperInvariant()}' AND Slug = 'pinned'
            """);

        var body = await GetCandidatesAsync(project.Id);

        var row = body!.Candidates.ShouldHaveSingleItem();
        row.PinnedBranch.ShouldBe("feat/235-orphan");
        row.PinnedWorktreePath.ShouldBe("/lanes/235");
        row.ClaimState.ShouldBe("ended");
        row.ClaimedByAlias.ShouldBe("dan");
    }

    [Fact]
    public async Task Door_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/agent/projects/{Guid.NewGuid()}/planning/rescue-candidates");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Takeover_ForeignAlias_WritesNotificationCenterInfo()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "abandoned");
        await BringToStatusAsync(project.Id, "abandoned", "inProgress");
        await SeedSessionAsync("old-holder", "Dan", "qwen", DateTime.UtcNow.AddHours(-2));
        await SeedClaimAsync(project.Id, "abandoned", "Dan", "qwen", "old-holder", DateTime.UtcNow.AddHours(-2));

        var response = await SendClaimAsync(project.Id, "abandoned", "Bobby", "claude", Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notifications = await GetNotificationsAsync();
        var info = notifications.Items
            .Where(n => n.Kind == "info" && n.FeatureSlug == "abandoned")
            .ShouldHaveSingleItem();
        info.Text.ShouldContain("Bobby by claude");
        info.Text.ShouldContain("stale claim of 'abandoned' from Dan by qwen");
    }

    [Fact]
    public async Task Takeover_OwnAliasAdopt_WritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "mine");
        await BringToStatusAsync(project.Id, "mine", "inProgress");
        var seen = DateTime.UtcNow.AddHours(-2);
        await SeedSessionAsync("dead-holder", "Dan", "qwen", seen, seen);
        await SeedClaimAsync(project.Id, "mine", "Dan", "qwen", "dead-holder", seen);

        var response = await SendClaimAsync(project.Id, "mine", "Dan", "qwen", Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notifications = await GetNotificationsAsync();
        notifications.Items.ShouldNotContain(n => n.Kind == "info" && n.FeatureSlug == "mine");
    }

    [Fact]
    public async Task Takeover_SameAliasVendorMismatch_WritesNotificationCenterInfo()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "vendor-swap");
        await BringToStatusAsync(project.Id, "vendor-swap", "inProgress");
        await SeedSessionAsync("old-holder", "Dan", "claude", DateTime.UtcNow.AddHours(-2));
        await SeedClaimAsync(project.Id, "vendor-swap", "Dan", "claude", "old-holder", DateTime.UtcNow.AddHours(-2));

        var response = await SendClaimAsync(project.Id, "vendor-swap", "Dan", "qwen", Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notifications = await GetNotificationsAsync();
        var info = notifications.Items
            .Where(n => n.Kind == "info" && n.FeatureSlug == "vendor-swap")
            .ShouldHaveSingleItem();
        info.Text.ShouldContain("Dan by qwen");
        info.Text.ShouldContain("stale claim of 'vendor-swap' from Dan by claude");
    }

    [Fact]
    public async Task FreshClaim_WithNoFormerHolder_WritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "untouched");

        var response = await SendClaimAsync(project.Id, "untouched", "Bobby", "claude", Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var notifications = await GetNotificationsAsync();
        notifications.Items.ShouldNotContain(n => n.FeatureSlug == "untouched");
    }

    private async Task<HttpResponseMessage> SendClaimAsync(
        Guid projectId, string slug, string alias, string vendor, Guid sessionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/agent/projects/{projectId}/planning/features/{slug}/claim")
        {
            Content = JsonContent.Create(new { Alias = alias, Vendor = vendor }),
        };
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<NotificationsJson> GetNotificationsAsync()
    {
        var response = await _client.GetAsync("/api/notifications");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<NotificationsJson>(JsonOptions))!;
    }

    private async Task<RescueCandidatesJson?> GetCandidatesAsync(Guid projectId)
    {
        var response = await _client.GetAsync(
            $"/api/agent/projects/{projectId}/planning/rescue-candidates");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadCandidatesAsync(response);
    }

    private static async Task<RescueCandidatesJson?> ReadCandidatesAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<RescueCandidatesJson>(JsonOptions);

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"rescue-{Guid.NewGuid():N}";
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

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task BringToStatusAsync(Guid projectId, string slug, string status)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Status = status });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SeedSessionAsync(string key, string alias, string vendor, DateTime lastSeenAt, DateTime? endedAt = null)
    {
        var id = Guid.NewGuid();
        var ended = endedAt is { } at ? $"'{at:yyyy-MM-dd HH:mm:ss}'" : "NULL";
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', '{alias}', '{vendor}',
                    '{lastSeenAt.AddHours(-1):yyyy-MM-dd HH:mm:ss}', '{lastSeenAt:yyyy-MM-dd HH:mm:ss}', {ended})
            """);
        SessionIds[key] = id;
    }

    private async Task SeedClaimAsync(
        Guid projectId, string slug, string alias, string vendor, string sessionKey, DateTime claimedAt)
    {
        var sessionId = SessionIds[sessionKey];
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = '{alias}', ClaimedByVendor = '{vendor}',
                ClaimedAt = '{claimedAt:yyyy-MM-dd HH:mm:ss}',
                ClaimSessionId = '{sessionId.ToString().ToUpperInvariant()}'
            WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{slug}'
            """);
    }

    private async Task SeedSpanAsync(
        Guid projectId, string slug, string alias, string vendor, string sessionKey,
        DateTime claimedAt, DateTime? releasedAt)
    {
        var featureId = await ScalarAsync<string>(
            $"SELECT Id FROM PlanningFeatures WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{slug}'");
        var released = releasedAt is { } at ? $"'{at:yyyy-MM-dd HH:mm:ss}'" : "NULL";
        await ExecuteAsync($"""
            INSERT INTO FeatureClaimSpans (Id, ProjectId, FeatureId, Alias, Vendor, SessionId, ClaimedAt, ReleasedAt, ByHuman)
            VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}',
                    '{projectId.ToString().ToUpperInvariant()}', '{featureId}',
                    '{alias}', '{vendor}', '{SessionIds[sessionKey].ToString().ToUpperInvariant()}',
                    '{claimedAt:yyyy-MM-dd HH:mm:ss}', {released}, 0)
            """);
    }

    private readonly Dictionary<string, Guid> SessionIds = [];

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        var raw = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(raw!, typeof(T));
    }

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record RescueCandidatesJson(
        Guid ProjectId, DateTime GeneratedAt, RescueCandidateJson[] Candidates);

    public sealed record RescueCandidateJson(
        string Slug,
        string Title,
        string Status,
        string ClaimState,
        string ClaimedByAlias,
        string ClaimedByVendor,
        DateTime ClaimedAt,
        bool ClaimedByHuman,
        string? PinnedBranch,
        string? PinnedWorktreePath,
        DateTime UpdatedAt);

    public sealed record NotificationsJson(NotificationJson[] Items, int UnreadCount);

    public sealed record NotificationJson(string Kind, string? FeatureSlug, string Text);
}
