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

public sealed class PlanningBatchApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string FeatureSlug = "001-batch";

    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;
    private readonly string _databasePath;

    public PlanningBatchApiTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"overmind-batch-{Guid.NewGuid():N}.db");
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
    public async Task Batch_ForeignLiveClaim_RefusesWith409NamingHolderAndPersistsNothing()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var taskId = await CreateTaskArtifactAsync(projectId);
        await SeedClaimAsync(projectId, HolderSessionId);

        var response = await PostBatchAsync(
            projectId,
            $$"""{"ops":[{"op":"append_log","artifactId":"{{taskId}}","note":"must not persist"}]}""",
            ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["title"]!.GetValue<string>().ShouldBe("FeatureClaimHeld");
        problem["detail"]!.GetValue<string>().ShouldContain("Van");

        var artifact = await ReadTaskAsync(taskId);
        artifact!["version"]!.GetValue<int>().ShouldBe(1);
        artifact["body"]!.GetValue<string>().ShouldNotContain("must not persist");
    }

    [Fact]
    public async Task Batch_SetStatusWithoutLogNote_Returns400NamingOpsIndexAndPersistsNothing()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var taskId = await CreateTaskArtifactAsync(projectId);

        var response = await PostBatchAsync(
            projectId,
            $$"""
            {"ops":[
                {"op":"append_log","artifactId":"{{taskId}}","note":"pre-status note"},
                {"op":"set_status","artifactId":"{{taskId}}","status":"done"}
            ]}
            """,
            sessionId: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["detail"]!.GetValue<string>().ShouldStartWith("ops[1]:");

        var artifact = await ReadTaskAsync(taskId);
        artifact!["version"]!.GetValue<int>().ShouldBe(1);
        artifact["body"]!.GetValue<string>().ShouldNotContain("pre-status note");
    }

    [Fact]
    public async Task Batch_OperationIdReplay_ReturnsStoredResponseWithoutReexecution()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var taskId = await CreateTaskArtifactAsync(projectId);
        var payload = $$"""{"ops":[{"op":"append_log","artifactId":"{{taskId}}","note":"first batch note"}]}""";
        var operationId = Guid.NewGuid();

        var first = await PostBatchAsync(projectId, payload, sessionId: null, operationId);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var original = await first.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        original!["replayed"].ShouldBeNull();
        var storedVersion = original["results"]![0]!["payload"]!["version"]!.GetValue<int>();

        await SeedClaimAsync(projectId, ForeignSessionId);

        var replay = await PostBatchAsync(projectId, payload, sessionId: null, operationId);

        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await replay.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        body!["replayed"]!.GetValue<bool>().ShouldBeTrue();
        body["featureSlug"]!.GetValue<string>().ShouldBe(FeatureSlug);
        body["results"]![0]!["payload"]!["version"]!.GetValue<int>().ShouldBe(storedVersion);
        (await ReadTaskAsync(taskId))!["version"]!.GetValue<int>().ShouldBe(storedVersion);
    }

    [Fact]
    public async Task Batch_NotifyEcho_CarriesDeepLinkWithTheBatchFeatureSlugDefault()
    {
        var projectId = await CreateProjectWithFeatureAsync();

        var response = await PostBatchAsync(
            projectId,
            """{"ops":[{"op":"notify","kind":"question","text":"ship it?","tab":"tasks"}]}""",
            sessionId: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        var payload = body!["results"]![0]!["payload"]!;
        payload["featureSlug"]!.GetValue<string>().ShouldBe(FeatureSlug);
        payload["url"]!.GetValue<string>().ShouldContain($"feature={FeatureSlug}");
        payload["url"]!.GetValue<string>().ShouldContain("tab=tasks");
    }

    [Fact]
    public async Task Batch_HappyPath_ReturnsOrderedPerOpResultsWithAliasAddressing()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var taskId = await CreateTaskArtifactAsync(projectId);

        var response = await PostBatchAsync(
            projectId,
            $$$"""
            {"ops":[
                {"op":"patch","alias":"{{{FeatureSlug}}}/T0","patch":{"op":"tick_ac","acIndex":1,"logNote":"ticked"}},
                {"op":"append_log","alias":"T0","note":"closing"},
                {"op":"set_status","artifactId":"{{{taskId}}}","status":"done","logNote":"task closed"}
            ]}
            """,
            sessionId: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        body!["featureSlug"]!.GetValue<string>().ShouldBe(FeatureSlug);
        var results = body["results"]!.AsArray();
        results.Count.ShouldBe(3);

        results[0]!["op"]!.GetValue<string>().ShouldBe("patch");
        results[0]!["index"]!.GetValue<int>().ShouldBe(0);
        results[0]!["payload"]!["section"]!.GetValue<string>().ShouldBe("acceptance-criteria");
        results[0]!["payload"]!["tickedIndex"]!.GetValue<int>().ShouldBe(1);

        results[1]!["op"]!.GetValue<string>().ShouldBe("append_log");
        results[1]!["index"]!.GetValue<int>().ShouldBe(1);
        results[1]!["payload"]!["id"]!.GetValue<Guid>().ShouldBe(taskId);

        results[2]!["op"]!.GetValue<string>().ShouldBe("set_status");
        results[2]!["index"]!.GetValue<int>().ShouldBe(2);
        results[2]!["payload"]!["status"]!.GetValue<string>().ShouldBe("done");
        results[2]!["payload"]!["id"]!.GetValue<Guid>().ShouldBe(taskId);

        var artifact = await ReadTaskAsync(taskId);
        artifact!["status"]!.GetValue<string>().ShouldBe("done");
        artifact["body"]!.GetValue<string>().ShouldContain("[x] first item done");
        artifact["body"]!.GetValue<string>().ShouldContain("closing");
    }

    [Fact]
    public async Task Batch_UnresolvableAlias_FailsFastBeforeAnyOpExecutes()
    {
        var projectId = await CreateProjectWithFeatureAsync();

        var response = await PostBatchAsync(
            projectId,
            """
            {"ops":[
                {"op":"append_log","alias":"T9","note":"nope"},
                {"op":"upsert_tasks","tasks":[{"body":"# Late\n\nlate task body\n"}]}
            ]}
            """,
            sessionId: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["detail"]!.GetValue<string>().ShouldStartWith("ops[0]:");
        problem["detail"]!.GetValue<string>().ShouldContain("No artifact matches alias");

        var roster = await _client.GetFromJsonAsync<JsonObject>(
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/artifacts", JsonOptions);
        roster!["artifacts"]!.AsArray().Count.ShouldBe(0);
    }

    [Fact]
    public async Task Batch_UnknownArtifactId_Returns400NamingOpsIndex()
    {
        var projectId = await CreateProjectWithFeatureAsync();
        var unknown = Guid.NewGuid();

        var response = await PostBatchAsync(
            projectId,
            $$"""{"ops":[{"op":"append_log","artifactId":"{{unknown}}","note":"nope"}]}""",
            sessionId: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["detail"]!.GetValue<string>().ShouldStartWith("ops[0]:");
        problem["detail"]!.GetValue<string>().ShouldContain(unknown.ToString());
    }

    [Fact]
    public async Task Batch_NullTaskEntry_Returns400NamingOpsIndexAndPersistsNothing()
    {
        var projectId = await CreateProjectWithFeatureAsync();

        var response = await PostBatchAsync(
            projectId,
            """
            {"ops":[
                {"op":"upsert_tasks","tasks":[{"body":"# Early\n\nearly task body\n"}, null]}
            ]}
            """,
            sessionId: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        problem!["detail"]!.GetValue<string>().ShouldStartWith("ops[0]:");
        problem["detail"]!.GetValue<string>().ShouldContain("tasks[] entry");

        var roster = await _client.GetFromJsonAsync<JsonObject>(
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/artifacts", JsonOptions);
        roster!["artifacts"]!.AsArray().Count.ShouldBe(0);
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

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> CreateProjectWithFeatureAsync()
    {
        var name = $"batch-{Guid.NewGuid():N}";
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
            new { Slug = FeatureSlug, Title = "Batch", Body = "" });
        feature.StatusCode.ShouldBe(HttpStatusCode.Created);

        return projectId;
    }

    private async Task<Guid> CreateTaskArtifactAsync(Guid projectId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/artifacts",
            new
            {
                Kind = "task",
                Order = 0,
                Body = "# T0 — batch parity\n\n<acceptance-criteria>\n- [ ] first item done\n- [ ] second item done\n</acceptance-criteria>\n",
                LogNote = "seed",
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var artifact = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        return Guid.Parse(artifact!["id"]!.GetValue<string>());
    }

    private async Task<JsonObject?> ReadTaskAsync(Guid taskId) =>
        await _client.GetFromJsonAsync<JsonObject>($"/api/agent/planning/artifacts/{taskId}", JsonOptions);

    private async Task<HttpResponseMessage> PostBatchAsync(Guid projectId, string json, Guid? sessionId) =>
        await PostBatchAsync(projectId, json, sessionId, operationId: null);

    private async Task<HttpResponseMessage> PostBatchAsync(
        Guid projectId, string json, Guid? sessionId, Guid? operationId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/agent/projects/{projectId}/planning/features/{FeatureSlug}/batch")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (operationId is { } operation)
        {
            request.Headers.Add(OperationReplayFilter.HeaderName, operation.ToString());
        }

        if (sessionId is { } session)
        {
            request.Headers.Add(SessionHeader.Name, session.ToString());
        }

        return await _client.SendAsync(request);
    }
}
