using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class UsageIngestApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    public UsageIngestApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-usage-ingest-{Guid.NewGuid():N}.db");
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
    public async Task Ingest_UnknownIdentity_StoresRecordUnattributed()
    {
        var response = await PostUsageAsync(alias: "ghost", sessionId: Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var stored = await ReadStoredAsync();
        stored.Alias.ShouldBe("ghost");
        stored.Model.ShouldBe("claude-haiku-4-5");
        stored.InputTokens.ShouldBe(100L);
    }

    [Fact]
    public async Task Ingest_BlankAlias_Returns400NamingTheField()
    {
        var response = await PostUsageAsync(alias: "  ", sessionId: Guid.NewGuid());

        await AssertFieldErrorAsync(response, "alias");
    }

    [Fact]
    public async Task Ingest_OverlongAlias_Returns400NamingTheField()
    {
        var response = await PostUsageAsync(alias: new string('x', 33), sessionId: Guid.NewGuid());

        await AssertFieldErrorAsync(response, "alias");
    }

    [Fact]
    public async Task Ingest_EmptySessionId_Returns400NamingTheField()
    {
        var response = await PostUsageAsync(alias: "Dan", sessionId: Guid.Empty);

        await AssertFieldErrorAsync(response, "sessionId");
    }

    [Fact]
    public async Task Ingest_DefaultTimestamp_Returns400NamingTheField()
    {
        var response = await PostUsageAsync(
            alias: "Dan", sessionId: Guid.NewGuid(), ts: DateTime.MinValue);

        await AssertFieldErrorAsync(response, "ts");
    }

    [Fact]
    public async Task Ingest_BlankModel_Returns400NamingTheField()
    {
        var response = await PostUsageAsync(alias: "Dan", sessionId: Guid.NewGuid(), model: "");

        await AssertFieldErrorAsync(response, "model");
    }

    [Theory]
    [InlineData("inputTokens", -1, 0, 0, 0)]
    [InlineData("outputTokens", 0, -1, 0, 0)]
    [InlineData("cacheReadTokens", 0, 0, -1, 0)]
    [InlineData("cacheWriteTokens", 0, 0, 0, -1)]
    public async Task Ingest_NegativeTokenCount_Returns400NamingTheField(
        string field, long input, long output, long cacheRead, long cacheWrite)
    {
        var response = await PostUsageAsync(
            alias: "Dan", sessionId: Guid.NewGuid(),
            inputTokens: input, outputTokens: output, cacheReadTokens: cacheRead, cacheWriteTokens: cacheWrite);

        await AssertFieldErrorAsync(response, field);
    }

    private async Task<HttpResponseMessage> PostUsageAsync(
        string alias,
        Guid sessionId,
        DateTime? ts = null,
        string model = "claude-haiku-4-5",
        long inputTokens = 100,
        long outputTokens = 50,
        long cacheReadTokens = 10,
        long cacheWriteTokens = 5)
    {
        return await _client.PostAsJsonAsync("/api/usage", new
        {
            alias,
            sessionId,
            ts = ts ?? DateTime.UtcNow,
            model,
            inputTokens,
            outputTokens,
            cacheReadTokens,
            cacheWriteTokens
        });
    }

    private async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["detail"]!
            .GetValue<string>();
        detail.ShouldContain(field, Case.Sensitive);
    }

    private async Task<(string Alias, string Model, long InputTokens)> ReadStoredAsync()
    {
        using var command = _db.CreateCommand();
        command.CommandText = "SELECT Alias, Model, InputTokens FROM UsageRecords";
        using var reader = await command.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();
        return (reader.GetString(0), reader.GetString(1), reader.GetInt64(2));
    }
}
