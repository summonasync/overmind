using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ClaimLockExemptionApiTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();
    private static readonly Guid ForeignSessionId = Guid.NewGuid();
    private const string HolderIdentity = "Dan by qwen (alive)";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ClaimLockExemptionApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-exempt-{Guid.NewGuid():N}.db");
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

    public static TheoryData<string> LockedMutations => new(
    [
        "set_feature_status", "update_feature", "queue_move", "set_feature_priority", "pin_feature_code",
        "add_feature_dependency", "remove_feature_dependency", "upsert_artifact", "upsert_tasks",
        "set_artifact_status", "append_log", "edit_log", "patch_artifact", "validate", "delete_artifact",
    ]);

    [Theory]
    [MemberData(nameof(LockedMutations))]
    public async Task LockedMutation_ForeignSession_StillReturns409(string mutation)
    {
        var project = await CreateProjectAsync();
        var (slug, artifactId, blockerSlug) = await SeedClaimedFeatureAsync(project.Id, mutation);

        var response = await SendAsync(mutation, project.Id, slug, artifactId, blockerSlug, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("FeatureClaimHeld");
        problem.Detail!.ShouldContain($"claim held by {HolderIdentity}");
    }

    [Fact]
    public async Task AppendDirective_ForeignSession_OnLiveClaimedFeature_Returns200()
    {
        var project = await CreateProjectAsync();
        var (slug, _, _) = await SeedClaimedFeatureAsync(project.Id, "append_directive");

        var response = await SendAsync("append_directive", project.Id, slug, Guid.Empty, null, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var directive = await response.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions);
        directive!.Text.ShouldBe("HOLD: shared file inbound");
        directive.AuthorAlias.ShouldBe("Rider");
        directive.AuthorVendor.ShouldBe("claude");
        directive.Ack.ShouldBeFalse();
    }

    [Fact]
    public async Task AckDirective_ForeignSession_OnLiveClaimedFeature_Returns200()
    {
        var project = await CreateProjectAsync();
        var (slug, _, _) = await SeedClaimedFeatureAsync(project.Id, "append_directive");
        var append = await SendAsync("append_directive", project.Id, slug, Guid.Empty, null, ForeignSessionId);
        var directive = await append.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions);

        var response = await SendAsync("ack_directive", project.Id, slug, directive!.Id, null, ForeignSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ack = await response.Content.ReadFromJsonAsync<AckJson>(JsonOptions);
        ack!.Ack.ShouldBeTrue();
    }

    [Fact]
    public async Task DirectiveEndpoints_HeaderlessHumanDoor_NeverLocks()
    {
        var project = await CreateProjectAsync();
        var (slug, _, _) = await SeedClaimedFeatureAsync(project.Id, "append_directive");

        var append = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/{slug}/directives",
            new { Text = "human note", Alias = "Dana", Vendor = "human" }, JsonOptions);
        append.StatusCode.ShouldBe(HttpStatusCode.OK);
        var directive = await append.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions);
        directive!.AuthorAlias.ShouldBe("Dana");
        directive.AuthorVendor.ShouldBe("human");

        var ack = await _client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/planning/features/{slug}/directives/{directive.Id}/ack",
            new { Alias = "Dana" }, JsonOptions);
        ack.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ackBody = await ack.Content.ReadFromJsonAsync<AckJson>(JsonOptions);
        ackBody!.Ack.ShouldBeTrue();
        ackBody.AckByAlias.ShouldBe("Dana");
    }

    private async Task<HttpResponseMessage> SendAsync(
        string mutation, Guid projectId, string slug, Guid artifactId, string? blockerSlug, Guid sessionId)
    {
        await SeedSessionAsync(ForeignSessionId, "Rider", "claude");
        var featureUrl = $"/api/agent/projects/{projectId}/planning/features/{slug}";
        var artifactUrl = $"/api/agent/planning/artifacts/{artifactId}";
        var (method, url, body) = mutation switch
        {
            "append_directive" => (HttpMethod.Post, $"{featureUrl}/directives",
                (object)new { Text = "HOLD: shared file inbound" }),
            "ack_directive" => (HttpMethod.Post, $"{featureUrl}/directives/{artifactId}/ack", null),
            "set_feature_status" => (HttpMethod.Post, $"{featureUrl}/status",
                new { Status = "inProgress", LogNote = "moving" }),
            "update_feature" => (HttpMethod.Post, $"{featureUrl}/update",
                new { Title = "Renamed", LogNote = "rename" }),
            "queue_move" => (HttpMethod.Post, $"{featureUrl}/queue-move", new { Move = "last" }),
            "set_feature_priority" => (HttpMethod.Post, $"{featureUrl}/priority", new { Priority = "p1" }),
            "pin_feature_code" => (HttpMethod.Post, $"{featureUrl}/pin-code",
                new { Branch = "feat/exempt-branch" }),
            "add_feature_dependency" => (HttpMethod.Post, $"{featureUrl}/dependencies",
                new { BlockedBy = blockerSlug }),
            "remove_feature_dependency" => (HttpMethod.Delete, $"{featureUrl}/dependencies/{blockerSlug}", null),
            "upsert_artifact" => (HttpMethod.Post, $"{featureUrl}/artifacts",
                new { Kind = "spec", Body = "spec body v2" }),
            "upsert_tasks" => (HttpMethod.Post, $"{featureUrl}/artifacts/tasks",
                new { Tasks = new[] { new { Body = "# extra\n\n<goal>extra</goal>", Order = 2 } } }),
            "set_artifact_status" => (HttpMethod.Post, $"{artifactUrl}/status",
                new { Status = "validated", LogNote = "validating" }),
            "append_log" => (HttpMethod.Post, $"{artifactUrl}/log", new { Note = "log line" }),
            "edit_log" => (HttpMethod.Put, $"{artifactUrl}/log", new { Text = "edited line" }),
            "patch_artifact" => (HttpMethod.Post, $"{artifactUrl}/patch", new { Op = "set_brief", Brief = "brief" }),
            "validate" => (HttpMethod.Post, $"{artifactUrl}/validate", new { AsMigrated = true, LogNote = "pre-approved" }),
            "delete_artifact" => (HttpMethod.Delete, artifactUrl, new { LogNote = "removing" }),
            _ => throw new ArgumentException($"Unknown mutation '{mutation}'.", nameof(mutation)),
        };

        var request = new HttpRequestMessage(method, url);
        if (sessionId != Guid.Empty)
        {
            request.Headers.Add(SessionHeader.Name, sessionId.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<(string Slug, Guid ArtifactId, string BlockerSlug)> SeedClaimedFeatureAsync(
        Guid projectId, string slugSuffix)
    {
        var slug = $"exempt-{slugSuffix.Replace('_', '-')}";
        var blockerSlug = $"blocker-{slugSuffix.Replace('_', '-')}";
        await CreateAgentFeatureAsync(projectId, slug);
        await CreateAgentFeatureAsync(projectId, blockerSlug);
        var spec = await CreateArtifactAsync(projectId, slug, new { Kind = "spec", Body = "spec body" });
        await CreateArtifactAsync(projectId, slug, new
        {
            Kind = "task",
            Order = 1,
            Body = $"# {slug}\n\n<goal>do the thing</goal>",
        });

        await SeedSessionAsync(HolderSessionId, "Dan", "qwen");
        await ExecuteAsync($"""
            UPDATE PlanningFeatures
            SET ClaimedByAlias = 'Dan',
                ClaimedByVendor = 'qwen',
                ClaimedAt = '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}',
                ClaimSessionId = '{HolderSessionId.ToString().ToUpperInvariant()}'
            WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}' AND Slug = '{slug}'
            """);

        return (slug, spec, blockerSlug);
    }

    private async Task SeedSessionAsync(Guid sessionId, string alias, string vendor)
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{sessionId.ToString().ToUpperInvariant()}', '{alias}', '{vendor}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
    }

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, object body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts", body, JsonOptions);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var raw = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return raw.GetProperty("id").GetGuid();
    }

    private async Task CreateAgentFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"exempt-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record DirectiveJson(
        Guid Id, string Text, string? AuthorAlias, string? AuthorVendor, DateTime CreatedAt, bool Ack);

    private sealed record AckJson(Guid Id, bool Ack, string? AckByAlias, string? AckVendor, DateTime? AckAt);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
