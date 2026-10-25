using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class WaveParticipantsApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    public WaveParticipantsApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-participants-{Guid.NewGuid():N}.db");
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
    public async Task Get_HoldersOnTwoLanes_ReturnsOwnerFirstThenAgentsByLaneName()
    {
        var projectId = await CreateProjectAsync(_client);
        await ClaimLaneFeatureAsync(_client, projectId, 1, "web", "guldan", "claude");
        await ClaimLaneFeatureAsync(_client, projectId, 2, "backend", "marshal", "qwen");

        var response = await _client.GetAsync($"/api/projects/{projectId}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await ReadAsync(response);
        roster.Items.Length.ShouldBe(3);
        roster.Items[0].ShouldBe(new ParticipantJson("owner", null, "owner", null, null, "owner"));
        roster.Items[1].ShouldBe(new ParticipantJson("agent", "backend", "marshal", "qwen", "alive", "worker"));
        roster.Items[2].ShouldBe(new ParticipantJson("agent", "web", "guldan", "claude", "alive", "worker"));
    }

    [Fact]
    public async Task Get_MarshalSessionHoldingALane_AgentRowCarriesTheSessionRole()
    {
        var projectId = await CreateProjectAsync(_client);
        await ClaimLaneFeatureAsync(_client, projectId, 1, "backend", "Marshal", "claude");
        await ExecuteAsync("UPDATE AgentSessions SET Role = 'marshal' WHERE Alias = 'Marshal'");

        var roster = await ReadAsync(
            await _client.GetAsync($"/api/projects/{projectId}/wave/participants"));

        roster.Items.Length.ShouldBe(2);
        roster.Items[0].ShouldBe(new ParticipantJson("owner", null, "owner", null, null, "owner"));
        roster.Items[1].ShouldBe(
            new ParticipantJson("agent", "backend", "Marshal", "claude", "alive", "marshal"));
    }

    [Fact]
    public async Task Get_ClaimReleased_AgentDropsOwnerStays()
    {
        var projectId = await CreateProjectAsync(_client);
        await ClaimLaneFeatureAsync(_client, projectId, 1, "backend", "guldan", "claude");
        await _client.PostAsync(
            $"/api/projects/{projectId}/planning/features/001-backend-work/unclaim", null);

        var response = await _client.GetAsync($"/api/projects/{projectId}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await ReadAsync(response);
        roster.Items.ShouldBe([new ParticipantJson("owner", null, "owner", null, null, "owner")]);
    }

    [Fact]
    public async Task Get_AllLanesUnclaimed_ReturnsOwnerOnlyWithoutErrors()
    {
        var projectId = await CreateProjectAsync(_client);
        await CreateLaneAsync(_client, projectId, "backend");
        await CreateLaneAsync(_client, projectId, "web");

        var response = await _client.GetAsync($"/api/projects/{projectId}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await ReadAsync(response);
        roster.Items.ShouldBe([new ParticipantJson("owner", null, "owner", null, null, "owner")]);
    }

    [Fact]
    public async Task Get_UnknownProject_Returns404WithNamedCode()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        problem["title"]!.GetValue<string>().ShouldBe("MissingProject");
    }

    [Fact]
    public async Task Get_AgentFace_MatchesHumanFace()
    {
        var projectId = await CreateProjectAsync(_client);
        await ClaimLaneFeatureAsync(_client, projectId, 1, "backend", "guldan", "claude");

        var human = await ReadAsync(
            await _client.GetAsync($"/api/projects/{projectId}/wave/participants"));
        var agent = await ReadAsync(
            await _client.GetAsync($"/api/agent/projects/{projectId}/wave/participants"));

        agent.Items.ShouldBe(human.Items);
        agent.Items.ShouldContain(p => p.Alias == "guldan");
    }

    [Fact]
    public async Task Get_CustomOwnerAlias_ConfiguredAliasWins()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Wave:OwnerAlias"] = "marshal",
                })));
        var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client);

        var response = await client.GetAsync($"/api/projects/{projectId}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await ReadAsync(response);
        roster.Items[0].ShouldBe(new ParticipantJson("owner", null, "marshal", null, null, "owner"));
    }

    [Fact]
    public async Task Get_BlankOwnerAliasConfig_FallsBackToOwner()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Wave:OwnerAlias"] = "   ",
                })));
        var client = factory.CreateClient();
        var projectId = await CreateProjectAsync(client);

        var response = await client.GetAsync($"/api/projects/{projectId}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roster = await ReadAsync(response);
        roster.Items[0].Alias.ShouldBe("owner");
    }

    [Fact]
    public async Task Get_AfterParticipantsDoor_ChatSurfacesStillServe()
    {
        var projectId = await CreateProjectAsync(_client);
        await CreateLaneAsync(_client, projectId, "backend");

        var post = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/wave/rooms/backend/messages",
            new { AuthorAlias = "Dan", Text = "lane talk" }, JsonOptions);
        var rooms = await _client.GetAsync($"/api/projects/{projectId}/wave/rooms");
        var participants = await _client.GetAsync($"/api/projects/{projectId}/wave/participants");

        post.StatusCode.ShouldBe(HttpStatusCode.OK);
        rooms.StatusCode.ShouldBe(HttpStatusCode.OK);
        participants.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_ClaimedLane_AgentRowCarriesSessionLastSeenAndOwnerStaysNull()
    {
        var projectId = await CreateProjectAsync(_client);
        await ClaimLaneFeatureAsync(_client, projectId, 1, "backend", "guldan", "claude");

        var response = await _client.GetAsync($"/api/projects/{projectId}/wave/participants");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var items = json["items"]!.AsArray();
        items.Count.ShouldBe(2);
        items[0]!["lastSeenAt"].ShouldBeNull();
        var seenAt = DateTimeOffset.Parse(items[1]!["lastSeenAt"]!.GetValue<string>());
        seenAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        seenAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(1));
    }

    private static async Task ClaimLaneFeatureAsync(
        HttpClient client, Guid projectId, int number, string lane, string alias, string vendor)
    {
        await CreateLaneAsync(client, projectId, lane);

        var slug = $"{number:000}-{lane}-work";
        var create = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = "Lane work" }, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bind = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Lane = lane }, JsonOptions);
        bind.StatusCode.ShouldBe(HttpStatusCode.OK);

        var claim = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/claim",
            new { Alias = alias, Vendor = vendor }, JsonOptions);
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var name = $"wave-participants-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task CreateLaneAsync(
        HttpClient client, Guid projectId, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes",
            new { Name = name, Contour = (string?)null, SerialOrder = (int?)null, Priority = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<ParticipantsJson> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ParticipantsJson>(JsonOptions))!;

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ParticipantJson(
        string Kind, string? Lane, string Alias, string? Vendor, string? ClaimState, string Role);

    private sealed record ParticipantsJson(ParticipantJson[] Items);
}
