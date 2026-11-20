using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private async Task<Guid> SeedCallerSessionAsync(string alias, string vendor, DateTime lastSeenAt)
    {
        var callerId = Guid.NewGuid();
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{callerId.ToString().ToUpperInvariant()}', '{alias}', '{vendor}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{lastSeenAt:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
        return callerId;
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

    private async Task SeedCloseWithTapAsync(Guid projectId, string slug)
    {
        await CreateArtifactAsync(projectId, slug, new { Kind = "result", Body = "close-out" });

        var create = await _client.PostAsJsonAsync("/api/agent/notifications", new
        {
            Kind = "approval",
            Text = $"please approve {slug}",
            ProjectId = projectId,
            FeatureSlug = slug
        }, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var notificationId = JsonNode.Parse(await create.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var reply = await _client.PostAsJsonAsync(
            $"/api/notifications/{notificationId}/reply", new { Text = "lgtm" });
        reply.StatusCode.ShouldBe(HttpStatusCode.OK);
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
        "set_status", "append_log", "patch_artifact", "validate", "delete_artifact",
        "pin_feature_code",
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
        await SeedCloseWithTapAsync(projectId, slug);

        var featureUrl = $"/api/agent/projects/{projectId}/planning/features/{slug}";
        var (method, url, body) = mutation switch
        {
            "set_feature_status" => (HttpMethod.Post, $"{featureUrl}/status",
                (object)new { Status = "done", LogNote = "closing" }),
            "update_feature" => (HttpMethod.Post, $"{featureUrl}/update",
                new { Title = "Renamed", LogNote = "rename" }),
            "set_feature_priority" => (HttpMethod.Post, $"{featureUrl}/priority",
                new { Priority = "p1" }),
            "pin_feature_code" => (HttpMethod.Post, $"{featureUrl}/pin-code",
                new { Branch = "feat/lock-branch" }),
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
            "delete_artifact" => (HttpMethod.Delete, $"/api/agent/planning/artifacts/{task}",
                new { LogNote = "removing" }),
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
    public async Task Claim_AgentDoor_ClaimsAndReturnsEcho()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow);

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = "Bobby", Vendor = "claude" }, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        echo!.Slug.ShouldBe("claimable");
        echo.ClaimedByAlias.ShouldBe("Bobby");
        echo.ClaimedByVendor.ShouldBe("claude");
        echo.ClaimState.ShouldBe("alive");
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE Slug = 'claimable' AND ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Claim_AgentDoor_WithoutHeader_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = "Bobby", Vendor = "claude" }, null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Claim_AgentDoor_IdentityOverCap_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = new string('a', 33), Vendor = "claude" }, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Claim_AgentDoor_LiveForeignHolder_Returns409NamingHolder()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = "Bobby", Vendor = "claude" }, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureClaimHeld");
        problem.Detail!.ShouldContain($"claim held by {HolderIdentity}");
    }

    [Fact]
    public async Task Claim_AgentDoor_StaleForeignHolder_TakesOver()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow.AddHours(-2));
        await ClaimFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = "Bobby", Vendor = "claude" }, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        echo!.ClaimedByAlias.ShouldBe("Bobby");
        echo.ClaimState.ShouldBe("alive");
    }

    [Fact]
    public async Task Claim_AgentDoor_ZeroIdentity_BindsTheSessionIdentity()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        var callerId = await SeedCallerSessionAsync("Tucker", "qwen", DateTime.UtcNow);

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/claim",
            new { }, callerId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        echo!.ClaimedByAlias.ShouldBe("Tucker");
        echo.ClaimedByVendor.ShouldBe("qwen");
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE Slug = 'claimable' AND ClaimSessionId = '{callerId.ToString().ToUpperInvariant()}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Write_StaleSelfClaimOnSameSession_ReadoptsAndLogs()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow.AddHours(-2));
        await ClaimFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/status",
            new { Status = "inProgress", LogNote = "resuming" }, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/claimable", JsonOptions);
        feature!.Body.ShouldContain("stale claim re-adopted by resumed session Dan by qwen");
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE Slug = 'claimable' AND ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Write_StaleSameAliasForeignSession_StillRefused()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow.AddHours(-2));
        await ClaimFeatureAsync(project.Id, "claimable");
        var callerId = await SeedCallerSessionAsync("Dan", "qwen", DateTime.UtcNow);

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/status",
            new { Status = "inProgress", LogNote = "resuming" }, callerId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE Slug = 'claimable' AND ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Write_AliveSelfIdentityClaim_StillRefused()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "claimable");
        var callerId = await SeedCallerSessionAsync("Dan", "qwen", DateTime.UtcNow);

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/status",
            new { Status = "inProgress", LogNote = "resuming" }, callerId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureClaimHeld");
        problem.Detail!.ShouldContain("claim held by Dan by qwen (alive)");
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE Slug = 'claimable' AND ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Write_StaleForeignClaim_StillRefused()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow.AddHours(-2));
        await ClaimFeatureAsync(project.Id, "claimable");
        var callerId = await SeedCallerSessionAsync("Bobby", "claude", DateTime.UtcNow);

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/status",
            new { Status = "inProgress", LogNote = "resuming" }, callerId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureClaimHeld");
        problem.Detail!.ShouldContain("claim held by Dan by qwen (stale)");
        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/claimable", JsonOptions);
        feature!.Body.ShouldNotContain("session restarted");
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE Slug = 'claimable' AND ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Unclaim_AgentDoor_ByHolder_Releases()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/unclaim",
            null, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        echo!.ClaimedByAlias.ShouldBeNull();
        echo.ClaimState.ShouldBeNull();
    }

    [Fact]
    public async Task Unclaim_AgentDoor_ForeignSession_Returns409()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "claimable");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/features/claimable/unclaim",
            null, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail!.ShouldContain($"claim held by {HolderIdentity}");
    }

    [Fact]
    public async Task HumanDoor_Claim_OnLiveAgentClaim_TakesOver()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "claimable");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = "Dana", Vendor = "human" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        echo!.ClaimedByAlias.ShouldBe("Dana");
        echo.ClaimedByVendor.ShouldBe("human");
        echo.ClaimState.ShouldBe("alive");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/claimable", JsonOptions);
        feature!.Body.ShouldContain("took over alive claim of Dan by qwen");
    }

    [Fact]
    public async Task HumanDoor_ClaimAndUnclaim_WorkWithoutSessionHeaders()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "claimable");

        var claim = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/claimable/claim",
            new { Alias = "Dan", Vendor = "qwen" }, JsonOptions);
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);
        var claimEcho = await claim.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        claimEcho!.ClaimedByAlias.ShouldBe("Dan");
        claimEcho.ClaimState.ShouldBe("alive");
        claimEcho.ClaimedByHuman.ShouldBeTrue();

        var unclaim = await _client.PostAsync(
            $"/api/projects/{project.Id}/planning/features/claimable/unclaim", null);
        unclaim.StatusCode.ShouldBe(HttpStatusCode.OK);
        var unclaimEcho = await unclaim.Content.ReadFromJsonAsync<ClaimJson>(JsonOptions);
        unclaimEcho!.ClaimedByAlias.ShouldBeNull();
    }

    [Fact]
    public async Task ReadPayloads_CarryClaimFields()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "read-claims");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "read-claims");

        var board = await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/projects/{project.Id}/planning/features", JsonOptions);
        var boarded = board!.Single(i => i.Slug == "read-claims");
        boarded.ClaimedByAlias.ShouldBe("Dan");
        boarded.ClaimedByVendor.ShouldBe("qwen");
        boarded.ClaimState.ShouldBe("alive");
        boarded.ClaimedByHuman.ShouldBeFalse();

        var queue = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/task-queue", null, HolderSessionId);
        var queueBody = await queue.Content.ReadFromJsonAsync<QueueResponseJson>(JsonOptions);
        var queued = queueBody!.Items.Single(i => i.Slug == "read-claims");
        queued.ClaimedByAlias.ShouldBe("Dan");
        queued.ClaimState.ShouldBe("alive");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/read-claims", JsonOptions);
        feature!.ClaimedByAlias.ShouldBe("Dan");
        feature.ClaimState.ShouldBe("alive");
    }

    [Fact]
    public async Task Reorder_Echo_CarriesClaimFieldsForClaimedFeature()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "reorder-claimed");
        await CreateAgentFeatureAsync(project.Id, "reorder-other");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "reorder-claimed");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/projects/{project.Id}/planning/queue/reorder",
            new { Priority = "p2", Slugs = new[] { "reorder-claimed", "reorder-other" } }, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<QueueResponseJson>(JsonOptions);
        var claimed = body!.Items.Single(i => i.Slug == "reorder-claimed");
        claimed.ClaimedByAlias.ShouldBe("Dan");
        claimed.ClaimState.ShouldBe("alive");
        var other = body.Items.Single(i => i.Slug == "reorder-other");
        other.ClaimedByAlias.ShouldBeNull();
        other.ClaimState.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_NamingRules_ProjectValueAndGenericHint()
    {
        var project = await CreateProjectAsync();

        var generic = await _client.PostAsJsonAsync(
            "/api/onboard", new { Cwd = project.DiskPath }, JsonOptions);
        var genericBody = await generic.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        genericBody.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldContain("invent an alias");
        genericBody.GetProperty("agentIdentity").GetProperty("alias").GetString()!.ShouldBe("agent");

        await ExecuteAsync(
            $"UPDATE Projects SET AgentNamingRules = 'aliases are crew names, vendor is the runtime' WHERE Id = '{project.Id.ToString().ToUpperInvariant()}'");

        var custom = await _client.PostAsJsonAsync(
            "/api/onboard", new { Cwd = project.DiskPath }, JsonOptions);
        var customBody = await custom.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        customBody.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldBe("aliases are crew names, vendor is the runtime");

        var put = await _client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/claim-settings",
            new { ClaimStaleThresholdMinutes = 25, AgentNamingRules = "aliases come from the crew, keep the vendor casing" },
            JsonOptions);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var apiDriven = await _client.PostAsJsonAsync(
            "/api/onboard", new { Cwd = project.DiskPath }, JsonOptions);
        var apiBody = await apiDriven.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        apiBody.GetProperty("agentIdentity").GetProperty("namingRules").GetString()!
            .ShouldBe("aliases come from the crew, keep the vendor casing");
    }

    [Fact]
    public async Task Import_ForeignClaimedFeature_Returns409AndWritesNothing()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "import-locked");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "import-locked");
        WriteImportFolder(project.DiskPath, "import-locked");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/import",
            new { Path = Path.Combine(project.DiskPath, "BACKLOG", "import-locked") }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureClaimHeld");
        problem.Detail!.ShouldContain($"claim held by {HolderIdentity}");
        (await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM PlanningFeatures WHERE ProjectId = '{project.Id.ToString().ToUpperInvariant()}' AND Slug = 'import-locked'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Import_OwnClaimedFeature_Passes()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "import-own");
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await ClaimFeatureAsync(project.Id, "import-own");
        WriteImportFolder(project.DiskPath, "import-own");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/import",
            new { Path = Path.Combine(project.DiskPath, "BACKLOG", "import-own") }, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Import_UnclaimedFeature_Passes()
    {
        var project = await CreateProjectAsync();
        WriteImportFolder(project.DiskPath, "import-free");

        var response = await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{project.Id}/planning/import",
            new { Path = Path.Combine(project.DiskPath, "BACKLOG", "import-free") }, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static void WriteImportFolder(string projectDiskPath, string slug)
    {
        var folder = Path.Combine(projectDiskPath, "BACKLOG", slug);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "feature.md"), $"""
            ---
            feature: {slug.Split('-')[0]}
            slug: {slug}
            status: draft
            version: 1
            created: {DateTime.UtcNow:yyyy-MM-dd}
            ---

            # {slug}

            Imported body.
            """);
    }

    [Fact]
    public async Task Mutation_HolderClosesDone_ReleasesClaim()
    {
        var project = await CreateProjectAsync();
        await SeedHolderSessionAsync(DateTime.UtcNow);
        await CreateAgentFeatureAsync(project.Id, "closable");
        await ClaimFeatureAsync(project.Id, "closable");
        await SeedCloseWithTapAsync(project.Id, "closable");

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

    private sealed record ClaimJson(
        string Slug, string? ClaimedByAlias, string? ClaimedByVendor,
        DateTime? ClaimedAt, string? ClaimState, bool ClaimedByHuman);

    private sealed record BoardItemJson(
        string Slug, string Title, string Status, string Priority, int Version,
        string? SpecStatus, string? PlanStatus, int TaskTotal, int TaskDone, DateTime UpdatedAt,
        bool IsBlocked, int QueuePosition,
        string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt, string? ClaimState,
        bool ClaimedByHuman);

    private sealed record QueueResponseJson(QueueItemJson[] Items);

    private sealed record QueueItemJson(
        string Slug, string? Number, string Title, string Priority, string Status, int QueuePosition,
        string? SpecStatus, string? PlanStatus, int TaskDone, int TaskTotal, int Readiness,
        string[] BlockedBy, bool IsBlocked,
        string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt, string? ClaimState);

    private sealed record FeatureJson(
        string Slug, string Title, string Body, string Status, string Priority, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, JsonElement[] Artifacts, bool IsBlocked,
        JsonElement[] BlockedBy, JsonElement[] Blocks,
        string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt, string? ClaimState);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
