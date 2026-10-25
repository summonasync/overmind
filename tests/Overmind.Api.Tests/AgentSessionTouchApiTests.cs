using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentSessionTouchApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    public AgentSessionTouchApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-touch-{Guid.NewGuid():N}.db");
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
    public async Task Touch_ActiveSession_AdvancesLastSeenAtAndKeepsSessionOpen()
    {
        var sessionId = await SeedSessionAsync(TimeSpan.FromMinutes(10), endedAgo: null);

        var response = await _client.PostAsync($"/api/agent/sessions/{sessionId}/touch", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var (lastSeenAt, endedAt) = await ReadTimestampsAsync(sessionId);
        DateTime.Parse(lastSeenAt!).ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
        endedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Touch_EndedSession_PreservesLastSeenAtAndEndedAt()
    {
        var sessionId = await SeedSessionAsync(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));
        var before = await ReadTimestampsAsync(sessionId);

        var response = await _client.PostAsync($"/api/agent/sessions/{sessionId}/touch", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = await ReadTimestampsAsync(sessionId);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Touch_UnknownSession_Returns404()
    {
        var response = await _client.PostAsync($"/api/agent/sessions/{Guid.NewGuid()}/touch", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain("was not found");
    }

    private async Task<Guid> SeedSessionAsync(TimeSpan lastSeenAgo, TimeSpan? endedAgo)
    {
        var id = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddMinutes(-30);
        await ExecuteAsync($"""
            INSERT INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', 'Dan', 'qwen',
                    '{startedAt:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow - lastSeenAgo:yyyy-MM-dd HH:mm:ss}', {FormatEnded(endedAgo)})
            """);
        return id;
    }

    private static string FormatEnded(TimeSpan? ago) =>
        ago is null ? "NULL" : $"'{DateTime.UtcNow - ago:yyyy-MM-dd HH:mm:ss}'";

    private async Task<(string? LastSeenAt, string? EndedAt)> ReadTimestampsAsync(Guid sessionId)
    {
        using var command = _db.CreateCommand();
        command.CommandText = $"SELECT LastSeenAt, EndedAt FROM AgentSessions WHERE Id = '{sessionId.ToString().ToUpperInvariant()}'";
        using var reader = await command.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();
        return (reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
