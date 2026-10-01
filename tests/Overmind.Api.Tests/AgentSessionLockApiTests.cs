using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentSessionLockApiTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();
    private const string HolderIdentity = "Dan by qwen (alive)";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;
    private readonly string _databasePath;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentSessionLockApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-session-{Guid.NewGuid():N}.db");
        _databasePath = databasePath;
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

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"session-lock-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateAgentFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task SeedHolderSessionAsync(DateTime lastSeenAt)
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{HolderSessionId.ToString().ToUpperInvariant()}', 'Dan', 'qwen', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{lastSeenAt:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
    }

    private async Task ClaimFeatureAsync(Guid projectId, string slug)
    {
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = 'Dan',
                ClaimedByVendor = 'qwen',
                ClaimedAt = '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}',
                ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'
            WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{slug}'
            """);
    }

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

    private HttpRequestMessage Request(HttpMethod method, string url, object? body, Guid? sessionId)
    {
        var request = new HttpRequestMessage(method, url);
        if (sessionId is { } id)
        {
            request.Headers.Add(SessionHeader.Name, id.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body, Guid? sessionId)
    {
        var response = await _client.SendAsync(Request(method, url, body, sessionId));
        return response;
    }

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, object body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts", body, JsonOptions);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var raw = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return raw.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Register_CreatesSessionRow()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = "Dan", Vendor = "qwen" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AgentSessionJson>(JsonOptions);
        body!.Id.ShouldNotBe(Guid.Empty);
        body.Alias.ShouldBe("Dan");
        body.Vendor.ShouldBe("qwen");
        body.EndedAt.ShouldBeNull();

        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM AgentSessions WHERE Id = '{body.Id.ToString().ToUpperInvariant()}' AND Alias = 'Dan' AND Vendor = 'qwen' AND EndedAt IS NULL"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Register_BlankAlias_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = " ", Vendor = "qwen" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_IdentityOverCap_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = new string('a', 33), Vendor = "qwen" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task End_MarksSessionEnded()
    {
        var registered = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = "Dan", Vendor = "qwen" }, JsonOptions);
        var session = (await registered.Content.ReadFromJsonAsync<AgentSessionJson>(JsonOptions))!;

        var response = await _client.PostAsync($"/api/agent/sessions/{session.Id}/end", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM AgentSessions WHERE Id = '{session.Id.ToString().ToUpperInvariant()}' AND EndedAt IS NOT NULL"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task End_UnknownSession_Returns404()
    {
        var response = await _client.PostAsync($"/api/agent/sessions/{Guid.NewGuid()}/end", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Touch_HeaderPresence_UpdatesLastSeenAt()
    {
        var project = await CreateProjectAsync();
        var staleSeen = DateTime.UtcNow.AddMinutes(-30);
        await SeedHolderSessionAsync(staleSeen);

        var response = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/features", null, HolderSessionId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await ScalarAsync<string>(
            $"SELECT LastSeenAt FROM AgentSessions WHERE Id = '{HolderSessionId.ToString().ToUpperInvariant()}'");
        DateTime.Parse(updated).ShouldBeGreaterThan(staleSeen);
    }

    [Fact]
    public async Task Touch_HeaderAbsence_LeavesLastSeenAt()
    {
        var project = await CreateProjectAsync();
        var staleSeen = DateTime.UtcNow.AddMinutes(-30);
        await SeedHolderSessionAsync(staleSeen);

        await _client.GetAsync($"/api/agent/projects/{project.Id}/planning/features");

        var updated = await ScalarAsync<string>(
            $"SELECT LastSeenAt FROM AgentSessions WHERE Id = '{HolderSessionId.ToString().ToUpperInvariant()}'");
        DateTime.Parse(updated).ShouldBe(staleSeen, TimeSpan.FromSeconds(1));
    }

    public static TheoryData<string> MutationNames => new(
    [
        "set_feature_status", "update_feature", "set_feature_priority", "move_feature",
        "add_feature_dependency", "remove_feature_dependency", "upsert_artifact", "upsert_tasks",
        "set_status", "append_log", "patch_artifact", "validate",
    ]);

    private async Task<(HttpResponseMessage Response, Guid SpecId, Guid TaskId, string BlockerSlug)> SendMutationAsync(
        string mutation, Guid projectId, Guid? sessionId)
    {
        await SeedHolderSessionAsync(DateTime.UtcNow);
        var slug = $"lock-{mutation.Replace('_', '-')}";
        var blockerSlug = $"blocker-{mutation.Replace('_', '-')}";
        await CreateAgentFeatureAsync(projectId, slug);
        await CreateAgentFeatureAsync(projectId, blockerSlug);
        var spec = await CreateArtifactAsync(projectId, slug, new { Kind = "spec", Body = "spec body" });
        var task = await CreateArtifactAsync(projectId, slug, new
        {
            Kind = "task",
            Order = 1,
            Body = $"# {slug}\n\n<goal>do the thing</goal>",
        });
        await ClaimFeatureAsync(projectId, slug);

        var featureUrl = $"/api/agent/projects/{projectId}/planning/features/{slug}";
        var (method, url, body) = mutation switch
        {
            "set_feature_status" => (HttpMethod.Post, $"{featureUrl}/status",
                (object)new { Status = "done", LogNote = "closing" }),
            "update_feature" => (HttpMethod.Post, $"{featureUrl}/update",
                new { Title = "Renamed", LogNote = "rename" }),
            "set_feature_priority" => (HttpMethod.Post, $"{featureUrl}/queue-move",
                new { Move = "down" }),
            "move_feature" => (HttpMethod.Post, $"{featureUrl}/queue-move",
                new { Move = "last" }),
            "add_feature_dependency" => (HttpMethod.Post, $"{featureUrl}/dependencies",
                new { BlockedBy = blockerSlug }),
            "remove_feature_dependency" => (HttpMethod.Delete, $"{featureUrl}/dependencies/{blockerSlug}", null),
            "upsert_artifact" => (HttpMethod.Post, $"{featureUrl}/artifacts",
                new { Kind = "spec", Body = "spec body v2" }),
            "upsert_tasks" => (HttpMethod.Post, $"{featureUrl}/artifacts/tasks",
                new { Tasks = new[] { new { Body = "# extra\n\n<goal>extra</goal>", Order = 2 } } }),
            "set_status" => (HttpMethod.Post, $"/api/agent/planning/artifacts/{spec}/status",
                new { Status = "validated", LogNote = "validating" }),
            "append_log" => (HttpMethod.Post, $"/api/agent/planning/artifacts/{spec}/log",
                new { Note = "log line" }),
            "patch_artifact" => (HttpMethod.Post, $"/api/agent/planning/artifacts/{task}/patch",
                new { Brief = "expedited" }),
            "validate" => (HttpMethod.Post, $"/api/agent/planning/artifacts/{spec}/validate",
                new { }),
            _ => throw new ArgumentException($"Unknown mutation '{mutation}'.", nameof(mutation)),
        };

        return (await SendAsync(method, url, body, sessionId), spec, task, blockerSlug);
    }

    [Theory]
    [MemberData(nameof(MutationNames))]
    public async Task Mutation_ForeignSession_Returns409NamingHolder(string mutation)
    {
        var project = await CreateProjectAsync();

        var (response, _, _, _) = await SendMutationAsync(mutation, project.Id, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureClaimHeld");
        problem.Detail!.ShouldContain($"claim held by {HolderIdentity}");
    }

    [Theory]
    [MemberData(nameof(MutationNames))]
    public async Task Mutation_HolderSession_IsNotLockedOut(string mutation)
    {
        var project = await CreateProjectAsync();
        await SeedHolderSessionAsync(DateTime.UtcNow);

        var (response, _, _, _) = await SendMutationAsync(mutation, project.Id, HolderSessionId);

        var problem = response.StatusCode == HttpStatusCode.Conflict
            ? await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions)
            : null;
        problem?.Title.ShouldNotBe("FeatureClaimHeld");
    }

    [Theory]
    [MemberData(nameof(MutationNames))]
    public async Task Mutation_Headerless_NeverLocks(string mutation)
    {
        var project = await CreateProjectAsync();

        var (response, _, _, _) = await SendMutationAsync(mutation, project.Id, null);

        var problem = response.StatusCode == HttpStatusCode.Conflict
            ? await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions)
            : null;
        problem?.Title.ShouldNotBe("FeatureClaimHeld");
    }

    [Fact]
    public async Task Reads_ForeignSession_StayOpen()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "read-open");
        var spec = await CreateArtifactAsync(project.Id, "read-open", new { Kind = "spec", Body = "spec body" });
        await ClaimFeatureAsync(project.Id, "read-open");

        var board = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/features", null, ForeignSessionId);
        var feature = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/features/read-open", null, ForeignSessionId);
        var artifacts = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/features/read-open/artifacts", null, ForeignSessionId);
        var artifact = await SendAsync(HttpMethod.Get,
            $"/api/agent/planning/artifacts/{spec}", null, ForeignSessionId);

        board.StatusCode.ShouldBe(HttpStatusCode.OK);
        feature.StatusCode.ShouldBe(HttpStatusCode.OK);
        artifacts.StatusCode.ShouldBe(HttpStatusCode.OK);
        artifact.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Mutation_HolderClosesDone_ReleasesClaim()
    {
        var project = await CreateProjectAsync();
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await CreateAgentFeatureAsync(project.Id, "closable");
        await ClaimFeatureAsync(project.Id, "closable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/closable/status",
            new { Status = "done", LogNote = "closing" }, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE ProjectId = '{project.Id.ToString().ToUpperInvariant()}' AND Slug = 'closable' AND ClaimSessionId IS NULL"))
            .ShouldBe(1);
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record AgentSessionJson(
        Guid Id, string Alias, string Vendor, DateTime StartedAt, DateTime LastSeenAt, DateTime? EndedAt);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
