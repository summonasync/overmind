using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Core;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentSessionIdentityApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentSessionIdentityApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-identity-{Guid.NewGuid():N}.db");
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
    public async Task SetIdentity_NewPair_PersistsAndReturnsTheSession()
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "Van", "qwen");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("session").GetProperty("alias").GetString().ShouldBe("Van");
        body.GetProperty("session").GetProperty("vendor").GetString().ShouldBe("qwen");
        body.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var (alias, vendor) = await ReadIdentityAsync(sessionId);
        alias.ShouldBe("Van");
        vendor.ShouldBe("qwen");
    }

    [Fact]
    public async Task SetIdentity_TrimmedValues_AreStoredTrimmed()
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "  Van  ", "  qwen  ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (alias, vendor) = await ReadIdentityAsync(sessionId);
        alias.ShouldBe("Van");
        vendor.ShouldBe("qwen");
    }

    [Fact]
    public async Task SetIdentity_AliasHeldByAliveSession_Returns409NamingTheHolder()
    {
        await SeedSessionAsync("Van", "qwen", lastSeenAgo: TimeSpan.Zero);
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "Van", "claude");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain("Van");
        detail.ShouldContain("qwen");
        var (alias, _) = await ReadIdentityAsync(sessionId);
        alias.ShouldBe("Dan");
    }

    [Fact]
    public async Task SetIdentity_ClaimGuardWritesNoRowsWithNoVisibleHolder_ReturnsTheRetryConflict()
    {
        var sessionId = await RegisterAsync("Dan", "claude");
        await ExecuteAsync($$"""
            CREATE TRIGGER identity_claim_silent_noop
            BEFORE UPDATE OF Alias ON AgentSessions
            FOR EACH ROW WHEN OLD.Id = '{{sessionId.ToString().ToUpperInvariant()}}'
            BEGIN SELECT RAISE(IGNORE); END
            """);

        var response = await SetIdentityAsync(sessionId, "Van", "qwen");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain("was claimed while this request was running");
        detail.ShouldNotContain("is held by");
        var (storedAlias, storedVendor) = await ReadIdentityAsync(sessionId);
        storedAlias.ShouldBe("Dan");
        storedVendor.ShouldBe("claude");
    }

    [Fact]
    public async Task SetIdentity_AliasHeldByStaleSession_Succeeds()
    {
        await SeedSessionAsync("Van", "qwen", lastSeenAgo: TimeSpan.FromMinutes(20));
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "Van", "claude");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (alias, _) = await ReadIdentityAsync(sessionId);
        alias.ShouldBe("Van");
    }

    [Fact]
    public async Task SetIdentity_AliasHeldByEndedSession_Succeeds()
    {
        await SeedSessionAsync("Van", "qwen", lastSeenAgo: TimeSpan.Zero, endedAgo: TimeSpan.FromMinutes(1));
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "Van", "claude");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (alias, _) = await ReadIdentityAsync(sessionId);
        alias.ShouldBe("Van");
    }

    [Fact]
    public async Task SetIdentity_CaseDifferenceAgainstAliveHolder_Succeeds()
    {
        await SeedSessionAsync("van", "qwen", lastSeenAgo: TimeSpan.Zero);
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "Van", "claude");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetIdentity_PlaceholderPair_SucceedsWithWarning()
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, "agent", "unknown");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var warnings = await ReadWarningsAsync(response);
        warnings.ShouldNotBeEmpty();
        warnings[0].ShouldContain("agent");
        warnings[0].ShouldContain("unknown");
    }

    // The 235 T7 m6 repro (issue ef10a2ff): the alias claim and the held-claim re-attribution must
    // commit together — a forced re-attribution failure rolls the alias back, and the retry heals.
    [Fact]
    public async Task SetIdentity_ReattributionFails_RollsBackAndARetryHeals()
    {
        var sessionId = await RegisterAsync("Dan", "claude");
        var claim = await SeedHeldClaimAsync(sessionId);

        await ExecuteAsync("""
            CREATE TRIGGER reattribution_abort
            BEFORE UPDATE OF ClaimedByAlias ON PlanningFeatures
            FOR EACH ROW WHEN NEW.ClaimedByAlias = 'Van'
            BEGIN SELECT RAISE(ABORT, 'reattribution forced failure'); END
            """);

        var failed = await SetIdentityAsync(sessionId, "Van", "qwen");

        failed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var detail = JsonNode.Parse(await failed.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain("Re-attribution failed");
        detail.ShouldContain("Dan");
        var (aliasAfterFailure, vendorAfterFailure) = await ReadIdentityAsync(sessionId);
        aliasAfterFailure.ShouldBe("Dan");
        vendorAfterFailure.ShouldBe("claude");
        (await ReadClaimAsync(claim)).Alias.ShouldBe("Dan");

        await ExecuteAsync("DROP TRIGGER reattribution_abort");
        var healed = await SetIdentityAsync(sessionId, "Van", "qwen");

        healed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (alias, vendor) = await ReadIdentityAsync(sessionId);
        alias.ShouldBe("Van");
        vendor.ShouldBe("qwen");
        var claimRow = await ReadClaimAsync(claim);
        claimRow.Alias.ShouldBe("Van");
        claimRow.Vendor.ShouldBe("qwen");
        claimRow.SessionId.ShouldBe(SessionIdText(sessionId));
        PlanningLog.Parse(claimRow.Body).ShouldContain(line =>
            line.Text == "claim re-attributed: Dan by claude → Van by qwen");
    }

    [Fact]
    public async Task SetIdentity_MidHoldChange_ReattributesHeldClaimsAndLogsTheMove()    {
        var sessionId = await RegisterAsync("Dan", "claude");
        var claim = await SeedHeldClaimAsync(sessionId);

        var response = await SetIdentityAsync(sessionId, "Van", "qwen");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await ReadClaimAsync(claim);
        stored.Alias.ShouldBe("Van");
        stored.Vendor.ShouldBe("qwen");
        stored.SessionId.ShouldBe(SessionIdText(sessionId));
        stored.ClaimedAt.ShouldBe(claim.ClaimedAt);

        var warnings = await ReadWarningsAsync(response);
        warnings.ShouldContain(w => w.Contains(claim.Slug) && w.Contains("logged the move"));
        warnings.ShouldNotContain(w => w.Contains("claim-time attribution"));
    }

    [Fact]
    public async Task SetIdentity_MidHoldChange_LogsOneReattributionLinePerFeature()
    {
        var sessionId = await RegisterAsync("Dan", "claude");
        var first = await SeedHeldClaimAsync(sessionId);
        var second = await SeedHeldClaimAsync(sessionId);

        await SetIdentityAsync(sessionId, "Van", "qwen");

        foreach (var claim in new[] { first, second })
        {
            var lines = PlanningLog.Parse((await ReadClaimAsync(claim)).Body);
            lines.ShouldContain(line =>
                line.Text == "claim re-attributed: Dan by claude → Van by qwen");
            lines.Count(line => line.Text.StartsWith("claim re-attributed", StringComparison.Ordinal))
                .ShouldBe(1, $"{claim.Slug} logged the move more than once");
        }
    }

    [Fact]
    public async Task SetIdentity_ForeignHolderSnapshot_IsLeftAlone()
    {
        var sessionId = await RegisterAsync("Dan", "claude");
        var mine = await SeedHeldClaimAsync(sessionId);
        var foreign = await SeedHeldClaimAsync(Guid.NewGuid());

        await SetIdentityAsync(sessionId, "Van", "qwen");

        (await ReadClaimAsync(foreign)).Alias.ShouldBe("Dan");
        (await ReadClaimAsync(mine)).Alias.ShouldBe("Van");
    }

    [Fact]
    public async Task SetIdentity_SameIdentityWhileHoldingClaims_NoWarning()
    {
        var sessionId = await RegisterAsync("Dan", "claude");
        var claim = await SeedHeldClaimAsync(sessionId);

        var response = await SetIdentityAsync(sessionId, "Dan", "claude");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var warnings = await ReadWarningsAsync(response);
        warnings.ShouldBeEmpty();
        PlanningLog.Parse((await ReadClaimAsync(claim)).Body).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "qwen")]
    [InlineData("   ", "qwen")]
    [InlineData("Van", "")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "qwen")]
    [InlineData("Van", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public async Task SetIdentity_EmptyOrOversize_Returns400(string alias, string vendor)
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await SetIdentityAsync(sessionId, alias, vendor);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (storedAlias, storedVendor) = await ReadIdentityAsync(sessionId);
        storedAlias.ShouldBe("Dan");
        storedVendor.ShouldBe("claude");
    }

    [Fact]
    public async Task SetIdentity_UnknownSession_Returns404()
    {
        var response = await SetIdentityAsync(Guid.NewGuid(), "Van", "qwen");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SetIdentityAsync(Guid sessionId, string alias, string vendor) =>
        await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{sessionId}/identity", new { Alias = alias, Vendor = vendor }, JsonOptions);

    private async Task<Guid> RegisterAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = alias, Vendor = vendor }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HeldClaim> SeedHeldClaimAsync(Guid sessionId)
    {
        var projectName = $"identity-hold-{Guid.NewGuid():N}";
        var projectResponse = await _client.PostAsJsonAsync(
            "/api/projects", new { Name = projectName, DiskPath = TestPaths.DiskPath(projectName) }, JsonOptions);
        projectResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await projectResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var projectId = project.GetProperty("id").GetGuid();

        var slug = $"hold-{Guid.NewGuid():N}".Substring(0, 16);
        var featureResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = "Identity hold", Body = string.Empty }, JsonOptions);
        featureResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var claimedAt = DateTime.UtcNow;
        var claimedAtText = Format(claimedAt);
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = 'Dan',
                ClaimedByVendor = 'claude',
                ClaimedAt = '{claimedAtText}',
                ClaimSessionId = '{SessionIdText(sessionId)}'
            WHERE ProjectId = '{SessionIdText(projectId)}' AND Slug = '{slug}'
            """);
        return new HeldClaim(projectId, slug, claimedAtText);
    }

    private async Task<HeldClaimRow> ReadClaimAsync(HeldClaim claim)
    {
        using var command = _db.CreateCommand();
        command.CommandText =
            $"SELECT ClaimedByAlias, ClaimedByVendor, ClaimSessionId, ClaimedAt, Body " +
            $"FROM PlanningFeatures WHERE ProjectId = '{SessionIdText(claim.ProjectId)}' AND Slug = '{claim.Slug}'";
        using var reader = await command.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue($"no feature row for '{claim.Slug}'");
        return new HeldClaimRow(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4));
    }

    private static string SessionIdText(Guid value) => value.ToString().ToUpperInvariant();

    private static string Format(DateTime value) =>
        $"{value:yyyy-MM-dd HH:mm:ss}.{value.Millisecond:000}";

    private sealed record HeldClaim(Guid ProjectId, string Slug, string ClaimedAt);

    private sealed record HeldClaimRow(
        string? Alias, string? Vendor, string? SessionId, string? ClaimedAt, string Body);

    private async Task SeedSessionAsync(
        string alias, string vendor, TimeSpan lastSeenAgo, TimeSpan? endedAgo = null)
    {
        var startedAt = DateTime.UtcNow.AddMinutes(-30);
        await ExecuteAsync($"""
            INSERT INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{alias}', '{vendor}',
                    '{startedAt:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow - lastSeenAgo:yyyy-MM-dd HH:mm:ss}',
                    {FormatEnded(endedAgo)})
            """);
    }

    private static string FormatEnded(TimeSpan? ago) =>
        ago is null ? "NULL" : $"'{DateTime.UtcNow - ago:yyyy-MM-dd HH:mm:ss}'";

    private async Task<(string Alias, string Vendor)> ReadIdentityAsync(Guid sessionId)
    {
        using var command = _db.CreateCommand();
        command.CommandText =
            $"SELECT Alias, Vendor FROM AgentSessions WHERE Id = '{sessionId.ToString().ToUpperInvariant()}'";
        using var reader = await command.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();
        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task<string[]> ReadWarningsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return [.. body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!)];
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
