using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentSessionRoleApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentSessionRoleApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-role-{Guid.NewGuid():N}.db");
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
    public async Task SetRole_Marshal_PersistsTheRole()
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{sessionId}/role", new { Role = "marshal" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadRoleAsync(sessionId)).ShouldBe("marshal");
    }

    [Fact]
    public async Task SetRole_Worker_RestoresTheDefault()
    {
        var sessionId = await RegisterAsync("Dan", "claude");
        await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{sessionId}/role", new { Role = "marshal" }, JsonOptions);

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{sessionId}/role", new { Role = "worker" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadRoleAsync(sessionId)).ShouldBe("worker");
    }

    [Fact]
    public async Task SetRole_Junk_Returns400NamingTheValidRoles()
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{sessionId}/role", new { Role = "wizard" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain("worker");
        detail.ShouldContain("marshal");
        (await ReadRoleAsync(sessionId)).ShouldBe("worker");
    }

    [Fact]
    public async Task SetRole_UnknownSession_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{Guid.NewGuid()}/role", new { Role = "marshal" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_WithRole_StoresIt()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = "Marshal", Vendor = "claude", Role = "marshal" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        (await ReadRoleAsync(session.GetProperty("id").GetGuid())).ShouldBe("marshal");
    }

    [Fact]
    public async Task Register_WithoutRole_DefaultsToWorker()
    {
        var sessionId = await RegisterAsync("Dan", "claude");

        (await ReadRoleAsync(sessionId)).ShouldBe("worker");
    }

    [Fact]
    public async Task Register_WithJunkRole_NormalizesToWorker()
    {
        var sessionId = await RegisterAsync("Dan", "claude", "archmage");

        (await ReadRoleAsync(sessionId)).ShouldBe("worker");
    }

    private async Task<Guid> RegisterAsync(string alias, string vendor, string? role = null)
    {
        var payload = new Dictionary<string, string?>
        {
            ["Alias"] = alias,
            ["Vendor"] = vendor,
            ["Role"] = role
        };

        var response = await _client.PostAsJsonAsync("/api/agent/sessions", payload, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<string> ReadRoleAsync(Guid sessionId)
    {
        using var command = _db.CreateCommand();
        command.CommandText = $"SELECT Role FROM AgentSessions WHERE Id = '{sessionId.ToString().ToUpperInvariant()}'";
        var result = await command.ExecuteScalarAsync();
        result.ShouldNotBeNull();
        return (string)result;
    }
}
