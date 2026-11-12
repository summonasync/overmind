using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api.Contracts;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class UsageSummaryApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;
    private readonly Guid _projectId;
    private readonly Guid _sessionA;
    private readonly Guid _sessionB;

    public UsageSummaryApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-usage-summary-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
        _projectId = SeedProjectAsync().GetAwaiter().GetResult();
        _sessionA = Guid.NewGuid();
        _sessionB = Guid.NewGuid();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Summary_RecordInsideOpenSpan_AttributesToFeatureAndAgent()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow.AddHours(-1), "test-model", 10, 20);

        var summary = await GetSummaryAsync();

        var feature = AssertSingleRow(summary.Features);
        feature.FeatureId.ShouldBe(featureId);
        feature.Slug.ShouldBe("001-alpha");
        feature.Records.ShouldBe(1L);
        feature.InputTokens.ShouldBe(10L);
        feature.OutputTokens.ShouldBe(20L);
        feature.Cost.ShouldBe(0m);
        var agent = AssertSingleRow(summary.Agents);
        agent.Alias.ShouldBe("Dan");
        agent.InputTokens.ShouldBe(10L);
    }

    [Fact]
    public async Task Summary_RecordsOutsideTheSpan_StayOutOfFeaturesButShowInAgentTotals()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow.AddHours(-3));
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow.AddMinutes(-30));

        var summary = await GetSummaryAsync();

        summary.Features.ShouldBeEmpty();
        var agent = AssertSingleRow(summary.Agents);
        agent.Alias.ShouldBe("Dan");
        agent.Records.ShouldBe(2L);
        agent.InputTokens.ShouldBe(200L);
    }

    [Fact]
    public async Task Summary_GapBetweenSequentialSpans_LeavesTheRecordUnattributed()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2));
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-1), null);
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow.AddMinutes(-90));
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow.AddMinutes(-30));

        var summary = await GetSummaryAsync();

        var feature = AssertSingleRow(summary.Features);
        feature.Records.ShouldBe(1L);
        feature.InputTokens.ShouldBe(100L);
    }

    [Fact]
    public async Task Summary_OverlappingSpansOfOneSession_MostRecentClaimWins()
    {
        var firstId = await SeedFeatureAsync("001-first");
        var secondId = await SeedFeatureAsync("002-second");
        await SeedSpanAsync(firstId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await SeedSpanAsync(secondId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-1), null);
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow);

        var summary = await GetSummaryAsync();

        var feature = AssertSingleRow(summary.Features);
        feature.FeatureId.ShouldBe(secondId);
        feature.Slug.ShouldBe("002-second");
    }

    [Fact]
    public async Task Summary_UnknownIdentityRecord_ShowsOnlyInAgentTotals()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await IngestAsync("ghost", _sessionB, DateTime.UtcNow.AddHours(-1));

        var summary = await GetSummaryAsync();

        summary.Features.ShouldBeEmpty();
        var agent = AssertSingleRow(summary.Agents);
        agent.Alias.ShouldBe("ghost");
        agent.InputTokens.ShouldBe(100L);
    }

    [Fact]
    public async Task Summary_HookRecordWithForeignSession_AttributesByAliasAndTime()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await IngestAsync("Dan", Guid.NewGuid(), DateTime.UtcNow.AddHours(-1));

        var summary = await GetSummaryAsync();

        var feature = AssertSingleRow(summary.Features);
        feature.FeatureId.ShouldBe(featureId);
        feature.Records.ShouldBe(1L);
        AssertSingleRow(summary.Agents).Alias.ShouldBe("Dan");
    }

    [Fact]
    public async Task Summary_HumanSpan_DoesNotAbsorbForeignSessionRecords()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "board", _sessionA, DateTime.UtcNow.AddHours(-2), null, byHuman: true);
        await IngestAsync("board", Guid.NewGuid(), DateTime.UtcNow.AddHours(-1));

        var summary = await GetSummaryAsync();

        summary.Features.ShouldBeEmpty();
        AssertSingleRow(summary.Agents).Alias.ShouldBe("board");
    }

    [Fact]
    public async Task Summary_ForeignSessionAcrossOverlappingSpans_PicksTheFreshestClaim()
    {
        var firstId = await SeedFeatureAsync("001-first");
        var secondId = await SeedFeatureAsync("002-second");
        await SeedSpanAsync(firstId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await SeedSpanAsync(secondId, "Dan", _sessionB, DateTime.UtcNow.AddHours(-1), null);
        await IngestAsync("Dan", Guid.NewGuid(), DateTime.UtcNow);

        var summary = await GetSummaryAsync();

        var feature = AssertSingleRow(summary.Features);
        feature.FeatureId.ShouldBe(secondId);
        AssertSingleRow(summary.Agents).Alias.ShouldBe("Dan");
    }

    [Fact]
    public async Task Summary_CostMatchesThePricingTable()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await IngestAsync(
            "Dan", _sessionA, DateTime.UtcNow.AddHours(-1),
            model: "claude-haiku-4-5",
            inputTokens: 1_000_000, outputTokens: 500_000,
            cacheReadTokens: 250_000, cacheWriteTokens: 100_000);

        var summary = await GetSummaryAsync();

        AssertSingleRow(summary.Features).Cost.ShouldBe(3.55m);
        AssertSingleRow(summary.Agents).Cost.ShouldBe(3.55m);
    }

    [Fact]
    public async Task Summary_CustomPricingFromSettings_IsAppliedAtReadTime()
    {
        var featureId = await SeedFeatureAsync("001-alpha");
        await SeedSpanAsync(featureId, "Dan", _sessionA, DateTime.UtcNow.AddHours(-2), null);
        await IngestAsync("Dan", _sessionA, DateTime.UtcNow.AddHours(-1), model: "glm-5", inputTokens: 1_000_000);

        var put = await _client.PutAsJsonAsync("/api/settings/usage-pricing", new
        {
            pricing = new Dictionary<string, object>
            {
                ["glm-5"] = new { input = 4.0, output = 0.0, cacheRead = 0.0, cacheWrite = 0.0 }
            }
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var summary = await GetSummaryAsync();

        AssertSingleRow(summary.Features).Cost.ShouldBe(4.00m);
        AssertSingleRow(summary.Agents).Cost.ShouldBe(4.00m);
    }

    [Fact]
    public async Task Summary_MissingProjectId_Returns400()
    {
        var response = await _client.GetAsync("/api/usage/summary");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static T AssertSingleRow<T>(IReadOnlyList<T> rows) where T : notnull
    {
        rows.Count.ShouldBe(1);
        return rows[0];
    }

    private async Task<UsageSummaryResponse> GetSummaryAsync() =>
        await _client.GetFromJsonAsync<UsageSummaryResponse>(
            $"/api/usage/summary?projectId={_projectId}");

    private async Task IngestAsync(
        string alias,
        Guid sessionId,
        DateTime ts,
        string model = "test-model",
        long inputTokens = 100,
        long outputTokens = 50,
        long cacheReadTokens = 0,
        long cacheWriteTokens = 0)
    {
        var response = await _client.PostAsJsonAsync("/api/usage", new
        {
            alias,
            sessionId,
            ts,
            model,
            inputTokens,
            outputTokens,
            cacheReadTokens,
            cacheWriteTokens
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> SeedProjectAsync()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        await ExecuteAsync($"""
            INSERT INTO Projects (Id, Name, DiskPath, Description, AgentNamingRules, CreatedAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', 'usage-proj-{id:N}', '/tmp/usage-proj', '', '', '{now}')
            """);
        return id;
    }

    private async Task<Guid> SeedFeatureAsync(string slug)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        await ExecuteAsync($"""
            INSERT INTO PlanningFeatures (Id, ProjectId, Slug, Title, Body, Status, Version, CreatedAt, UpdatedAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', '{_projectId.ToString().ToUpperInvariant()}', '{slug}', 'Title', '', 0, 1, '{now}', '{now}')
            """);
        return id;
    }

    private async Task SeedSpanAsync(
        Guid featureId, string alias, Guid sessionId, DateTime claimedAt,
        DateTime? releasedAt, bool byHuman = false)
    {
        var released = releasedAt is null
            ? "NULL"
            : $"'{releasedAt.Value:yyyy-MM-dd HH:mm:ss.fff}'";
        await ExecuteAsync($"""
            INSERT INTO FeatureClaimSpans (Id, ProjectId, FeatureId, Alias, Vendor, SessionId, ClaimedAt, ReleasedAt, ByHuman)
            VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{_projectId.ToString().ToUpperInvariant()}',
                    '{featureId.ToString().ToUpperInvariant()}', '{alias}', '{(byHuman ? "human" : "qwen")}', '{sessionId.ToString().ToUpperInvariant()}',
                    '{claimedAt:yyyy-MM-dd HH:mm:ss.fff}', {released}, {(byHuman ? 1 : 0)})
            """);
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
