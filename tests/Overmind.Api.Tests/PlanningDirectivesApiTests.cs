using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningDirectivesApiTests : IDisposable
{
    private static readonly Guid HolderSessionId = Guid.NewGuid();

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningDirectivesApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-directives-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
        SeedHolderSessionAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Append_AgentDoor_WithoutHeader_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/directed/directives",
            new { Text = "no identity" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Append_AgentDoor_UnknownSession_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var response = await SendAgentAppendAsync(
            project.Id, "directed", "ghost text", Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Append_AgentDoor_BlankText_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var response = await SendAgentAppendAsync(project.Id, "directed", "   ", HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Append_AgentDoor_TextOverCap_Returns400()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var response = await SendAgentAppendAsync(
            project.Id, "directed", new string('x', 501), HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Append_UnknownFeature_Returns404()
    {
        var project = await CreateProjectAsync();

        var response = await SendAgentAppendAsync(project.Id, "missing-feature", "text", HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Append_AgentDoor_TrimsAndAttributesFromTheSessionRow()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var response = await SendAgentAppendAsync(project.Id, "directed", "  SYNC on proxy.ts  ", HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var directive = await response.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions);
        directive!.Text.ShouldBe("SYNC on proxy.ts");
        directive.AuthorAlias.ShouldBe("Dan");
        directive.AuthorVendor.ShouldBe("qwen");
        directive.Ack.ShouldBeFalse();
    }

    [Fact]
    public async Task Ack_AgentDoor_SecondAckIsIdempotentNoOp()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");
        var appended = await SendAgentAppendAsync(project.Id, "directed", "HOLD", HolderSessionId);
        var directive = (await appended.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions))!;

        var first = await SendAgentAckAsync(project.Id, "directed", directive.Id, HolderSessionId);
        var second = await SendAgentAckAsync(project.Id, "directed", directive.Id, HolderSessionId);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<AckJson>(JsonOptions))!.Ack.ShouldBeTrue();
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<AckJson>(JsonOptions))!.Ack.ShouldBeFalse();
    }

    [Fact]
    public async Task Ack_UnknownDirective_Returns404()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var response = await SendAgentAckAsync(project.Id, "directed", Guid.NewGuid(), HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ack_DirectiveOfAnotherFeature_Returns404()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");
        await CreateAgentFeatureAsync(project.Id, "other-directed");
        var appended = await SendAgentAppendAsync(project.Id, "other-directed", "wrong channel", HolderSessionId);
        var directive = (await appended.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions))!;

        var response = await SendAgentAckAsync(project.Id, "directed", directive.Id, HolderSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FeatureRead_CarriesOpenDirectivesOldestFirst_AckedExcluded()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");
        var first = await AppendHumanAsync(project.Id, "directed", "first");
        await AppendHumanAsync(project.Id, "directed", "second");
        await SendAgentAckAsync(project.Id, "directed", first.Id, HolderSessionId);

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/projects/{project.Id}/planning/features/directed", JsonOptions);

        feature!.OpenDirectives.ShouldNotBeNull();
        feature.OpenDirectives.Select(d => d.Text).ShouldBe(["second"]);
        feature.OpenDirectives.ShouldNotContain(d => d.Id == first.Id);
    }

    [Fact]
    public async Task FeatureRead_CheapRosterPath_CarriesOpenDirectiveCountOnBothDoors()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");
        var acked = await AppendHumanAsync(project.Id, "directed", "one");
        await AppendHumanAsync(project.Id, "directed", "two");
        await SendAgentAppendAsync(project.Id, "directed", "three", HolderSessionId);
        await SendAgentAckAsync(project.Id, "directed", acked.Id, HolderSessionId);

        var humanRoster = JsonDocument.Parse(await _client.GetStringAsync(
            $"/api/projects/{project.Id}/planning/features/directed?includeArtifacts=false")).RootElement;
        var agentRoster = JsonDocument.Parse(await _client.GetStringAsync(
            $"/api/agent/projects/{project.Id}/planning/features/directed?includeArtifacts=false")).RootElement;

        humanRoster.GetProperty("openDirectiveCount").GetInt32().ShouldBe(2);
        agentRoster.GetProperty("openDirectiveCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task FeatureRead_CheapRosterPath_NoDirectives_CountIsZero()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var roster = JsonDocument.Parse(await _client.GetStringAsync(
            $"/api/projects/{project.Id}/planning/features/directed?includeArtifacts=false")).RootElement;

        roster.GetProperty("openDirectiveCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task FeatureRead_WithoutDirectives_CarriesEmptyOpenDirectives()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");

        var feature = await _client.GetFromJsonAsync<FeatureJson>(
            $"/api/agent/projects/{project.Id}/planning/features/directed", JsonOptions);

        feature!.OpenDirectives.ShouldBeEmpty();
    }

    [Fact]
    public async Task Current_CarriesOpenDirectivesOnTheFeatureSection()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "directed");
        var appended = await SendAgentAppendAsync(project.Id, "directed", "holder note", HolderSessionId);
        var directive = (await appended.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions))!;

        var current = await SendAgentCurrentAsync(project.Id, "directed");

        current.GetProperty("feature").GetProperty("openDirectives").GetArrayLength().ShouldBe(1);
        current.GetProperty("feature").GetProperty("openDirectives")[0]
            .GetProperty("id").GetString().ShouldBe(directive.Id.ToString());
    }

    [Fact]
    public async Task QueueAndBoardRows_CarryOpenDirectiveCount()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "counted");
        await CreateAgentFeatureAsync(project.Id, "uncounted");
        var first = await AppendHumanAsync(project.Id, "counted", "one");
        await AppendHumanAsync(project.Id, "counted", "two");
        await SendAgentAckAsync(project.Id, "counted", first.Id, HolderSessionId);

        var queue = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/task-queue", null, HolderSessionId);
        var queueBody = await queue.Content.ReadFromJsonAsync<QueueResponseJson>(JsonOptions);
        queueBody!.Items.Single(i => i.Slug == "counted").OpenDirectiveCount.ShouldBe(1);
        queueBody.Items.Single(i => i.Slug == "uncounted").OpenDirectiveCount.ShouldBe(0);

        var board = await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/projects/{project.Id}/planning/features", JsonOptions);
        board!.Single(i => i.Slug == "counted").OpenDirectiveCount.ShouldBe(1);
        board!.Single(i => i.Slug == "uncounted").OpenDirectiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task WaveSnapshot_InheritsOpenDirectiveCount()
    {
        var project = await CreateProjectAsync();
        await CreateAgentFeatureAsync(project.Id, "snapped");
        await AppendHumanAsync(project.Id, "snapped", "marshal note");

        var snapshot = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{project.Id}/planning/wave-snapshot", null, HolderSessionId);

        var body = JsonNode.Parse(await snapshot.Content.ReadAsStringAsync())!;
        var row = body["queue"]!.AsArray()
            .Single(i => i!["slug"]!.GetValue<string>() == "snapped")!;
        row["openDirectiveCount"]!.GetValue<int>().ShouldBe(1);
    }

    private async Task<HttpResponseMessage> SendAgentAppendAsync(
        Guid projectId, string slug, string text, Guid sessionId)
    {
        return await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/planning/features/{slug}/directives",
            new { Text = text }, sessionId);
    }

    private async Task<HttpResponseMessage> SendAgentAckAsync(
        Guid projectId, string slug, Guid directiveId, Guid sessionId)
    {
        return await SendAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/planning/features/{slug}/directives/{directiveId}/ack",
            null, sessionId);
    }

    private async Task<JsonElement> SendAgentCurrentAsync(Guid projectId, string slug)
    {
        var response = await SendAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/planning/features/{slug}/current", null, HolderSessionId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body, Guid sessionId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(SessionHeader.Name, sessionId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<DirectiveJson> AppendHumanAsync(Guid projectId, string slug, string text)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/directives",
            new { Text = text }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DirectiveJson>(JsonOptions))!;
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
        var name = $"directives-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task SeedHolderSessionAsync()
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{HolderSessionId.ToString().ToUpperInvariant()}', 'Dan', 'qwen', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
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

    private sealed record QueueResponseJson(QueueItemJson[] Items);

    private sealed record QueueItemJson(
        string Slug, string? Number, string Title, string Priority, string Status, int QueuePosition,
        string? SpecStatus, string? PlanStatus, int TaskDone, int TaskTotal, int Readiness,
        string[] BlockedBy, bool IsBlocked, int OpenDirectiveCount,
        string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt, string? ClaimState);

    private sealed record BoardItemJson(
        string Slug, string Title, string Status, string Priority, int Version,
        string? SpecStatus, string? PlanStatus, int TaskTotal, int TaskDone, DateTime UpdatedAt,
        bool IsBlocked, int QueuePosition, int OpenDirectiveCount,
        string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt, string? ClaimState,
        bool ClaimedByHuman);

    private sealed record OpenDirectiveJson(
        Guid Id, string Text, string? AuthorAlias, string? AuthorVendor, DateTime CreatedAt, bool Ack);

    private sealed record FeatureJson(
        string Slug, string Title, string Body, string Status, string Priority, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, JsonElement[] Artifacts, bool IsBlocked,
        JsonElement[] BlockedBy, JsonElement[] Blocks,
        string? ClaimedByAlias, string? ClaimedByVendor, DateTime? ClaimedAt, string? ClaimState,
        OpenDirectiveJson[] OpenDirectives);
}
