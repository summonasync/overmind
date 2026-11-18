using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentSessionIdentityRaceApiTests : IDisposable
{
    private const int Rounds = 25;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    public AgentSessionIdentityRaceApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-identity-race-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task SetIdentity_ConcurrentSameAlias_LeavesOneAliveHolder()
    {
        var bothWon = new List<string>();
        var losers = 0;
        for (var round = 0; round < Rounds; round++)
        {
            var alias = $"racer-{round}";
            var first = await RegisterAsync($"first-{round}", "claude");
            var second = await RegisterAsync($"second-{round}", "claude");

            var responses = await Task.WhenAll(
                SetIdentityAsync(first, alias, "claude"),
                SetIdentityAsync(second, alias, "claude"));

            if (responses.All(r => r.StatusCode == HttpStatusCode.OK))
            {
                bothWon.Add(alias);
            }

            // BothWon above only proves not-both-OK; every loser must also be told which alias it
            // lost and that a retry is the answer — that contract holds for both 409 variants.
            foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
            {
                losers++;
                var payload = await loser.Content.ReadAsStringAsync();
                loser.StatusCode.ShouldBe(HttpStatusCode.Conflict, payload);
                JsonNode.Parse(payload)!["detail"]!.GetValue<string>()
                    .ShouldContain(alias, customMessage: payload);
            }
        }

        var duplicated = await DuplicatedAliasOwnersAsync();

        (bothWon.Count, duplicated, losers)
            .ShouldBe((0, 0, Rounds),
                $"both claimed in {bothWon.Count} rounds, {duplicated} aliases end with >1 live row, "
                + $"{losers} losers instead of one per round");
    }

    private async Task<int> DuplicatedAliasOwnersAsync()
    {
        using var command = _db.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM (SELECT Alias FROM AgentSessions WHERE EndedAt IS NULL " +
            "GROUP BY Alias HAVING COUNT(*) > 1)";
        var count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count);
    }

    private async Task<Guid> RegisterAsync(string alias, string vendor)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = alias, Vendor = vendor }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SetIdentityAsync(Guid sessionId, string alias, string vendor) =>
        await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{sessionId}/identity", new { Alias = alias, Vendor = vendor }, JsonOptions);
}
