using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentSessionRegistrationApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    public AgentSessionRegistrationApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-registration-{Guid.NewGuid():N}.db");
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
    public async Task Register_PriorSessionIdOnPlaceholder_ResumesTheRowWithItsIdentity()
    {
        var priorId = await SeedSessionAsync("danny-lee", "claude", lastSeenAgo: TimeSpan.FromMinutes(40));

        var (firstId, firstAlias, firstVendor) = await RegisterAsync("agent", "unknown", priorId);

        firstId.ShouldBe(priorId);
        firstAlias.ShouldBe("danny-lee");
        firstVendor.ShouldBe("claude");
        var (lastSeenAt, endedAt) = await ReadLivenessAsync(priorId);
        endedAt.ShouldBeNull();
        DateTime.Parse(lastSeenAt).ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));

        var (secondId, secondAlias, _) = await RegisterAsync("agent", "unknown", priorId);
        secondId.ShouldBe(priorId);
        secondAlias.ShouldBe("danny-lee");
    }

    [Fact]
    public async Task Register_UnknownPriorSessionId_MintsFreshPlaceholderRow()
    {
        var unknownPrior = Guid.NewGuid();

        var (id, alias, vendor) = await RegisterAsync("agent", "unknown", unknownPrior);

        id.ShouldNotBe(unknownPrior);
        alias.ShouldBe("agent");
        vendor.ShouldBe("unknown");
    }

    [Fact]
    public async Task Register_ExplicitAlias_DoesNotResumeThePriorRow()
    {
        var priorId = await SeedSessionAsync("danny-lee", "claude", lastSeenAgo: TimeSpan.FromMinutes(40));

        var (id, alias, vendor) = await RegisterAsync("crew-otter", "claude", priorId);

        id.ShouldNotBe(priorId);
        alias.ShouldBe("crew-otter");
        vendor.ShouldBe("claude");
    }

    [Fact]
    public async Task Register_PriorAliasHeldByAliveRival_MintsFreshPlaceholder()
    {
        var priorId = await SeedSessionAsync("danny-lee", "claude", lastSeenAgo: TimeSpan.FromMinutes(40));
        await SeedSessionAsync("danny-lee", "claude", lastSeenAgo: TimeSpan.Zero);

        var (id, alias, vendor) = await RegisterAsync("agent", "unknown", priorId);

        id.ShouldNotBe(priorId);
        alias.ShouldBe("agent");
        vendor.ShouldBe("unknown");
    }

    [Fact]
    public async Task Register_ExplicitAliasHeldByAliveSession_Returns409NamingTheHolder()
    {
        await SeedSessionAsync("van-darkholme", "qwen", lastSeenAgo: TimeSpan.Zero);

        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions",
            new { Alias = "van-darkholme", Vendor = "claude" },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain("van-darkholme");
        detail.ShouldContain("qwen");
        (await ScalarCountAsync($"SELECT COUNT(*) FROM AgentSessions WHERE Alias = 'van-darkholme'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Register_ExplicitAliasHeldByStaleSession_Succeeds()
    {
        await SeedSessionAsync("van-darkholme", "qwen", lastSeenAgo: TimeSpan.FromMinutes(40));

        var (id, alias, vendor) = await RegisterAsync("van-darkholme", "claude", null);

        alias.ShouldBe("van-darkholme");
        vendor.ShouldBe("claude");
        var (_, endedAt) = await ReadLivenessAsync(id);
        endedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Register_ExplicitAliasHeldByEndedSession_Succeeds()
    {
        await SeedSessionAsync(
            "van-darkholme", "qwen", lastSeenAgo: TimeSpan.FromMinutes(40), endedAgo: TimeSpan.FromMinutes(2));

        var (id, alias, _) = await RegisterAsync("van-darkholme", "claude", null);

        alias.ShouldBe("van-darkholme");
        var (_, endedAt) = await ReadLivenessAsync(id);
        endedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Register_PriorRowIsPlaceholder_MintsFreshPlaceholder()
    {
        var priorId = await SeedSessionAsync("agent", "unknown", lastSeenAgo: TimeSpan.FromMinutes(40));

        var (id, alias, vendor) = await RegisterAsync("agent", "unknown", priorId);

        id.ShouldNotBe(priorId);
        alias.ShouldBe("agent");
        vendor.ShouldBe("unknown");
    }

    [Fact]
    public async Task Register_PriorRowEnded_ResumesIt()
    {
        var priorId = await SeedSessionAsync(
            "danny-lee", "claude", lastSeenAgo: TimeSpan.FromMinutes(40), endedAgo: TimeSpan.FromMinutes(2));

        var (id, alias, _) = await RegisterAsync("agent", "unknown", priorId);

        id.ShouldBe(priorId);
        alias.ShouldBe("danny-lee");
        var (_, endedAt) = await ReadLivenessAsync(priorId);
        endedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Register_ResumeWriteLostToConcurrentClaim_FallsBackToFresh()
    {
        var priorId = await SeedSessionAsync("danny-lee", "claude", lastSeenAgo: TimeSpan.FromMinutes(40));
        await ExecuteAsync($"""
            CREATE TRIGGER resume_silent_noop
            BEFORE UPDATE OF EndedAt ON AgentSessions
            FOR EACH ROW WHEN OLD.Id = '{priorId.ToString().ToUpperInvariant()}'
            BEGIN SELECT RAISE(IGNORE); END
            """);

        var (id, alias, vendor) = await RegisterAsync("agent", "unknown", priorId);

        id.ShouldNotBe(priorId);
        alias.ShouldBe("agent");
        vendor.ShouldBe("unknown");
    }

    private async Task<(Guid Id, string Alias, string Vendor)> RegisterAsync(
        string alias, string vendor, Guid? priorSessionId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions",
            new { Alias = alias, Vendor = vendor, PriorSessionId = priorSessionId },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (
            body.GetProperty("id").GetGuid(),
            body.GetProperty("alias").GetString()!,
            body.GetProperty("vendor").GetString()!);
    }

    private async Task<Guid> SeedSessionAsync(
        string alias, string vendor, TimeSpan lastSeenAgo, TimeSpan? endedAgo = null)
    {
        var id = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddMinutes(-30);
        await ExecuteAsync($"""
            INSERT INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', '{alias}', '{vendor}',
                    '{startedAt:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow - lastSeenAgo:yyyy-MM-dd HH:mm:ss}',
                    {FormatEnded(endedAgo)})
            """);
        return id;
    }

    private static string FormatEnded(TimeSpan? ago) =>
        ago is null ? "NULL" : $"'{DateTime.UtcNow - ago:yyyy-MM-dd HH:mm:ss}'";

    private async Task<(string LastSeenAt, string? EndedAt)> ReadLivenessAsync(Guid sessionId)
    {
        using var command = _db.CreateCommand();
        command.CommandText =
            $"SELECT LastSeenAt, EndedAt FROM AgentSessions WHERE Id = '{sessionId.ToString().ToUpperInvariant()}'";
        using var reader = await command.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> ScalarCountAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
