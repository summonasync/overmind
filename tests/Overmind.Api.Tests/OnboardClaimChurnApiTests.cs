using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Overmind.Host.Api.Contracts;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OnboardClaimChurnApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OnboardClaimChurnApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-churn-{Guid.NewGuid():N}.db");
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
    }

    [Fact]
    public async Task Onboard_EndedHolderStillHoldingClaims_ReportsSlugsAndDeathAge()
    {
        var projectId = await CreateProjectAsync();
        var dead = await SeedSessionAsync("mark-wolff", "claude", endedMinutesAgo: 20);
        await SeedClaimAsync(projectId, "010-churn-a", dead, "mark-wolff", "claude");
        await SeedClaimAsync(projectId, "011-churn-b", dead, "mark-wolff", "claude");

        var churn = ChurnOf(await OnboardAsync(Guid.NewGuid()));

        churn.ShouldNotBeNull();
        var row = churn.Sessions.ShouldHaveSingleItem();
        row.Alias.ShouldBe("mark-wolff");
        row.Vendor.ShouldBe("claude");
        row.Liveness.ShouldBe("ended");
        row.DeathAgeMinutes.ShouldBeInRange(19, 21);
        row.SlugsTotal.ShouldBe(2);
        row.Slugs.ShouldBe(["010-churn-a", "011-churn-b"]);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(90)]
    public async Task Onboard_StaleHolder_ReportsSilenceAge(int silentMinutes)
    {
        var projectId = await CreateProjectAsync();
        var stale = await SeedSessionAsync("van-darkholme", "qwen", lastSeenMinutesAgo: silentMinutes);
        await SeedClaimAsync(projectId, "012-stale-hold", stale, "van-darkholme", "qwen");

        var row = (ChurnOf(await OnboardAsync(Guid.NewGuid()))!).Sessions.ShouldHaveSingleItem();

        row.Liveness.ShouldBe("stale");
        row.DeathAgeMinutes.ShouldBeInRange(silentMinutes - 1, silentMinutes + 1);
    }

    [Fact]
    public async Task Onboard_CallerOwnClaims_AreNotChurn()
    {
        var projectId = await CreateProjectAsync();
        var caller = await SeedSessionAsync("dan", "claude", lastSeenMinutesAgo: 0);
        await SeedClaimAsync(projectId, "013-own-hold", caller, "dan", "claude");

        ChurnOf(await OnboardAsync(caller)).ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_SameAgedOutRowSeenByAnotherCaller_IsChurn()
    {
        // Liveness is the only rule here: asking refreshes the caller's own row (SessionTouchMiddleware),
        // so a session aged out is churn for everyone it is not — measured, not assumed: seeding the
        // same row 30m stale and asking as that caller leaves it alive again and the block absent.
        var projectId = await CreateProjectAsync();
        var aged = await SeedSessionAsync("dan", "claude", lastSeenMinutesAgo: 30);
        var slug = await SeedClaimAsync(projectId, "019-stale-other", aged, "dan", "claude");

        var row = (ChurnOf(await OnboardAsync(Guid.NewGuid()))!).Sessions.ShouldHaveSingleItem();

        row.Liveness.ShouldBe("stale");
        row.Slugs.ShouldBe([slug]);
    }

    [Fact]
    public async Task Onboard_AliveForeignHolder_IsNotChurn()
    {
        var projectId = await CreateProjectAsync();
        var alive = await SeedSessionAsync("billy-herrington", "qwen", lastSeenMinutesAgo: 0);
        await SeedClaimAsync(projectId, "014-alive-hold", alive, "billy-herrington", "qwen");

        (ChurnOf(await OnboardAsync(Guid.NewGuid()))).ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_ClaimOnADoneFeature_IsNotChurn()
    {
        var projectId = await CreateProjectAsync();
        var dead = await SeedSessionAsync("mark-wolff", "claude", endedMinutesAgo: 20);
        var slug = await SeedClaimAsync(projectId, "015-closed-hold", dead, "mark-wolff", "claude");
        await ExecuteAsync(
            $"UPDATE PlanningFeatures SET Status = 3 WHERE ProjectId = '{Text(projectId)}' AND Slug = '{slug}'");

        (ChurnOf(await OnboardAsync(Guid.NewGuid()))).ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_ManyClaimsOnOneHolder_CapsTheListAndReportsTheTotal()
    {
        var projectId = await CreateProjectAsync();
        var dead = await SeedSessionAsync("crew-otter", "qwen", endedMinutesAgo: 5);
        for (var i = 0; i < OnboardApi.ChurnSlugCap + 3; i++)
        {
            await SeedClaimAsync(projectId, $"cap-{i:D3}", dead, "crew-otter", "qwen");
        }

        var row = (ChurnOf(await OnboardAsync(Guid.NewGuid()))!).Sessions.ShouldHaveSingleItem();

        row.Slugs.Count.ShouldBe(OnboardApi.ChurnSlugCap);
        row.SlugsTotal.ShouldBe(OnboardApi.ChurnSlugCap + 3);
    }

    [Fact]
    public async Task Onboard_ManyDeadHolders_CapsTheRowsAndKeepsTheFreshest()
    {
        var projectId = await CreateProjectAsync();
        var ages = Enumerable.Range(1, OnboardApi.ChurnSessionCap + 2).ToArray();
        foreach (var age in ages)
        {
            var dead = await SeedSessionAsync($"gone-{age:D2}", "claude", endedMinutesAgo: age);
            await SeedClaimAsync(projectId, $"hold-{age:D2}", dead, $"gone-{age:D2}", "claude");
        }

        var churn = (ChurnOf(await OnboardAsync(Guid.NewGuid())))!;

        churn.Sessions.Count.ShouldBe(OnboardApi.ChurnSessionCap);
        churn.Sessions.ShouldContain(row => row.Alias == "gone-01");
        churn.Sessions.ShouldNotContain(row => row.Alias == "gone-08");
    }

    [Fact]
    public async Task Onboard_SessionHeaderWithARealPair_ReturnsTheEffectiveIdentity()
    {
        var projectId = await CreateProjectAsync();
        var caller = await SeedSessionAsync("mark-wolff", "claude", lastSeenMinutesAgo: 0);

        var identity = IdentityOf(await OnboardAsync(caller));

        identity.Alias.ShouldBe("mark-wolff");
        identity.Vendor.ShouldBe("claude");
    }

    [Fact]
    public async Task Onboard_WithoutASessionHeader_ReturnsThePlaceholderPair()
    {
        var projectId = await CreateProjectAsync();
        await CreateFeatureAsync(projectId, "016-no-claims");

        var identity = IdentityOf(await OnboardAsync(Guid.NewGuid()));

        identity.Alias.ShouldBe(AgentIdentityDefaults.Alias);
        identity.Vendor.ShouldBe(AgentIdentityDefaults.Vendor);
    }

    [Fact]
    public async Task Onboard_RenamedSession_ReturnsTheNewPairNotTheClaimTimeOne()
    {
        var projectId = await CreateProjectAsync();
        var caller = await SeedSessionAsync("dan", "claude", lastSeenMinutesAgo: 0);
        var slug = await SeedClaimAsync(projectId, "017-held-by-me", caller, "dan", "claude");

        var set = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{caller}/identity", new { Alias = "mister-po", Vendor = "qwen" }, JsonOptions);
        set.StatusCode.ShouldBe(HttpStatusCode.OK);

        IdentityOf(await OnboardAsync(caller)).Alias.ShouldBe("mister-po");
        (await ReadStringAsync(
            $"SELECT ClaimedByAlias FROM PlanningFeatures " +
            $"WHERE ProjectId = '{Text(projectId)}' AND Slug = '{slug}'")).ShouldBe("mister-po");
    }

    private async Task<JsonElement> OnboardAsync(Guid sessionId)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/onboard")
        {
            Content = JsonContent.Create(new { Cwd = _currentDiskPath }, options: JsonOptions),
        };
        message.Headers.Add(SessionHeader.Name, sessionId.ToString());

        var response = await _client.SendAsync(message);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var name = $"churn-{Guid.NewGuid():N}";
        _currentDiskPath = TestPaths.DiskPath(name);
        var response = await _client.PostAsJsonAsync(
            "/api/projects", new { Name = name, DiskPath = _currentDiskPath, Description = (string?)null }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return project.GetProperty("id").GetGuid();
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = string.Empty },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<string> SeedClaimAsync(
        Guid projectId, string slug, Guid sessionId, string alias, string vendor)
    {
        await CreateFeatureAsync(projectId, slug);
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = '{alias}',
                ClaimedByVendor = '{vendor}',
                ClaimedAt = '{Now()}',
                ClaimSessionId = '{Text(sessionId)}'
            WHERE ProjectId = '{Text(projectId)}' AND Slug = '{slug}'
            """);
        return slug;
    }

    private async Task<Guid> SeedSessionAsync(
        string alias,
        string vendor,
        int? lastSeenMinutesAgo = null,
        int? endedMinutesAgo = null)
    {
        var sessionId = Guid.NewGuid();
        var started = Now();
        var lastSeen = Text(DateTime.UtcNow.AddMinutes(-(lastSeenMinutesAgo ?? endedMinutesAgo ?? 0)));
        var ended = endedMinutesAgo is null ? "NULL" : $"'{Text(DateTime.UtcNow.AddMinutes(-endedMinutesAgo.Value))}'";

        await ExecuteAsync($"""
            INSERT INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{Text(sessionId)}', '{alias}', '{vendor}', '{started}', '{lastSeen}', {ended})
            """);
        return sessionId;
    }

    private static OnboardClaimChurnBlock? ChurnOf(JsonElement payload) =>
        payload.TryGetProperty("claimChurn", out var block) && block.ValueKind == JsonValueKind.Object
            ? block.Deserialize<OnboardClaimChurnBlock>(JsonOptions)
            : null;

    private static OnboardAgentIdentityBlock IdentityOf(JsonElement payload) =>
        payload.GetProperty("agentIdentity").Deserialize<OnboardAgentIdentityBlock>(JsonOptions)!;

    private async Task<string> ReadStringAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string Now() => Text(DateTime.UtcNow);

    private static string Text(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string Text(Guid value) => value.ToString().ToUpperInvariant();

    private string _currentDiskPath = string.Empty;
}
