using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OperationReplayApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;
    private readonly string _databasePath;

    public OperationReplayApiTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"overmind-op-replay-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(_databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={_databasePath};Pooling=false");
        _db.Open();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Replay_AfterClaimReleased_ReturnsStoredBodyWithoutReexecution()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var operationId = Guid.NewGuid();
        await SeedClaimAsync(projectId, HolderSessionId);

        var first = await PostStatusAsync(projectId, operationId, InProgressBody, HolderSessionId);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var original = await first.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        original!["replayed"].ShouldBeNull();
        var storedVersion = original["version"]!.GetValue<int>();

        await ClearClaimAsync(projectId);

        var replay = await PostStatusAsync(projectId, operationId, InProgressBody, sessionId: null);

        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await replay.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        body!["replayed"]!.GetValue<bool>().ShouldBeTrue();
        body["status"]!.GetValue<string>().ShouldBe("inProgress");
        body["slug"]!.GetValue<string>().ShouldBe(FeatureSlug);
        body["version"]!.GetValue<int>().ShouldBe(storedVersion);
        (await FeatureVersionAsync(projectId)).ShouldBe(storedVersion);
    }

    [Fact]
    public async Task Replay_WhileForeignClaimHolds_ShortCircuitsTheClaimLock()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var operationId = Guid.NewGuid();

        (await PostStatusAsync(projectId, operationId, InProgressBody, sessionId: null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        await SeedClaimAsync(projectId, ForeignSessionId);

        var replay = await PostStatusAsync(projectId, operationId, InProgressBody, sessionId: null);

        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await replay.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        body!["replayed"]!.GetValue<bool>().ShouldBeTrue();

        var fresh = await PostStatusAsync(projectId, Guid.NewGuid(), InProgressBody, HolderSessionId);
        fresh.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await fresh.Content.ReadFromJsonAsync<JsonObject>(JsonOptions))!["title"]!
            .GetValue<string>().ShouldBe("FeatureClaimHeld");
    }

    [Fact]
    public async Task SameOperationId_DifferentPayload_ReturnsOperationIdConflict()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var operationId = Guid.NewGuid();
        (await PostStatusAsync(projectId, operationId, InProgressBody, sessionId: null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var conflict = await PostStatusAsync(projectId, operationId, ReadyBody, sessionId: null);

        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["title"]!.GetValue<string>().ShouldBe("OperationIdConflict");
        (await FeatureVersionAsync(projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task NonGuidOperationId_ReturnsInvalidOperationId400()
    {
        var projectId = await CreateProjectWithFeatureAsync();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/status")
        {
            Content = new StringContent(InProgressBody, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(OperationReplayFilter.HeaderName, "operation-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["title"]!.GetValue<string>().ShouldBe("InvalidOperationId");
    }

    [Fact]
    public async Task FailedMutation_IsNotStored_AndFreshRetryExecutes()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var operationId = Guid.NewGuid();

        var forbidden = await PostStatusAsync(
            projectId, operationId, """{"status":"review","logNote":"agents may not"}""", sessionId: null);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var retry = await PostStatusAsync(projectId, operationId, InProgressBody, sessionId: null);

        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await retry.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        body!["replayed"].ShouldBeNull();
        body["status"]!.GetValue<string>().ShouldBe("inProgress");

        var replay = await PostStatusAsync(projectId, operationId, InProgressBody, sessionId: null);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<JsonObject>(JsonOptions))!["replayed"]!
            .GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExpiredRecords_ArePurgedOnInsert()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        await SeedExpiredRecordAsync(projectId, Guid.NewGuid());

        (await PostStatusAsync(projectId, Guid.NewGuid(), InProgressBody, sessionId: null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var count = await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningOperationRecords WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}'");
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Replay_OnArtifactDoor_ResolvesTheProjectThroughTheArtifact()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var artifactId = await CreateTaskArtifactAsync(projectId);
        var operationId = Guid.NewGuid();
        const string note = """{"note":"first append"}""";

        var first = await PostLogAsync(artifactId, operationId, note, sessionId: null);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var original = await first.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        var storedVersion = original!["version"]!.GetValue<int>();

        var replay = await PostLogAsync(artifactId, operationId, note, sessionId: null);

        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await replay.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        body!["replayed"]!.GetValue<bool>().ShouldBeTrue();
        body["version"]!.GetValue<int>().ShouldBe(storedVersion);
    }

    private const string FeatureSlug = "001-replay";
    private const string InProgressBody = """{"status":"inProgress","logNote":"first run"}""";
    private const string ReadyBody = """{"status":"ready","logNote":"second shape"}""";

    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private async Task<Guid> CreateProjectWithFeatureAsync()
    {
        var name = $"op-replay-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await created.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        var projectId = Guid.Parse(project!["id"]!.GetValue<string>());

        var feature = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = FeatureSlug, Title = "Replay", Body = "" });
        feature.StatusCode.ShouldBe(HttpStatusCode.Created);

        return projectId;
    }

    private async Task<Guid> CreateTaskArtifactAsync(Guid projectId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/artifacts",
            new { Kind = "task", Order = 0, Body = "<goal>replay probe</goal>", LogNote = "seed" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var artifact = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        return Guid.Parse(artifact!["id"]!.GetValue<string>());
    }

    private async Task<HttpResponseMessage> PostStatusAsync(
        Guid projectId, Guid operationId, string json, Guid? sessionId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/status")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        ApplyHeaders(request, sessionId, operationId);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostLogAsync(
        Guid artifactId, Guid operationId, string json, Guid? sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/agent/planning/artifacts/{artifactId}/log")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        ApplyHeaders(request, sessionId, operationId);
        return await _client.SendAsync(request);
    }

    private static void ApplyHeaders(HttpRequestMessage request, Guid? sessionId, Guid operationId)
    {
        if (sessionId is { } session)
        {
            request.Headers.Add(SessionHeader.Name, session.ToString());
        }

        request.Headers.Add(OperationReplayFilter.HeaderName, operationId.ToString());
    }

    private async Task SeedClaimAsync(Guid projectId, Guid sessionId)
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{sessionId.ToString().ToUpperInvariant()}', 'Van', 'qwen', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = 'Van',
                ClaimedByVendor = 'qwen',
                ClaimedAt = '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}',
                ClaimSessionId = '{sessionId.ToString().ToUpperInvariant()}'
            WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{FeatureSlug}'
            """);
    }

    private async Task ClearClaimAsync(Guid projectId) =>
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = NULL, ClaimedByVendor = NULL, ClaimedAt = NULL, ClaimSessionId = NULL
            WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{FeatureSlug}'
            """);

    private async Task SeedExpiredRecordAsync(Guid projectId, Guid operationId)
    {
        var createdAt = DateTime.UtcNow.AddHours(-48).ToString("yyyy-MM-dd HH:mm:ss");
        await ExecuteAsync(
            "INSERT INTO PlanningOperationRecords (Id, ProjectId, OperationId, RequestHash, StatusCode, ResponseBody, CreatedAt) " +
            $"VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{projectId.ToString().ToUpperInvariant()}', " +
            $"'{operationId.ToString().ToUpperInvariant()}', 'stale', 200, '{{}}', '{createdAt}')");
    }

    private async Task<int> FeatureVersionAsync(Guid projectId) =>
        await ScalarAsync<int>($"""
            SELECT Version FROM PlanningFeatures
            WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{FeatureSlug}'
            """);

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
}
