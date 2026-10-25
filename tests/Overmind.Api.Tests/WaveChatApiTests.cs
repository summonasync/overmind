using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class WaveChatApiTests : IDisposable
{
    private static readonly Guid AgentSessionId = Guid.NewGuid();

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WaveChatApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-wave-api-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
        SeedAgentSessionAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Post_HumanDoor_ReturnsMessageWithParsedMentions()
    {
        var projectId = await CreateProjectAsync();

        var response = await PostHumanAsync(projectId, "all", "Dan", "ping @marshal and @dan again");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);
        message!.Room.ShouldBe("all");
        message.AuthorAlias.ShouldBe("Dan");
        message.Text.ShouldBe("ping @marshal and @dan again");
        message.Mentions.ShouldBe(["marshal", "dan"]);
    }

    [Fact]
    public async Task Post_UnknownLane_Returns404WithNamedCode()
    {
        var projectId = await CreateProjectAsync();

        var response = await PostHumanAsync(projectId, "frontend", "Dan", "hello");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemTitleAsync(response)).ShouldBe("MissingWaveRoom");
    }

    [Fact]
    public async Task Post_AllRoom_IsValidOnAnyProject_EvenWithoutLanes()
    {
        var projectId = await CreateProjectAsync();

        var response = await PostHumanAsync(projectId, "ALL", "Dan", "hall talk");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions))!.Room.ShouldBe("all");
    }

    [Fact]
    public async Task Post_TextOverCap_Returns400WithNamedCode()
    {
        var projectId = await CreateProjectAsync();

        var response = await PostHumanAsync(projectId, "all", "Dan", new string('x', 501));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemTitleAsync(response)).ShouldBe("WaveTextTooLong");
    }

    [Fact]
    public async Task Post_BlankText_Returns400ValidationProblem()
    {
        var projectId = await CreateProjectAsync();

        var response = await PostHumanAsync(projectId, "all", "Dan", "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_AgentDoor_AttributesAuthorFromTheSessionRow()
    {
        var projectId = await CreateProjectAsync();

        var response = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages", new { Text = "  from the adapter  " });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);
        message!.AuthorAlias.ShouldBe("Dan");
        message.Text.ShouldBe("from the adapter");
    }

    [Fact]
    public async Task Post_AgentDoor_WithoutSession_Returns400()
    {
        var projectId = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages",
            new { Text = "no identity" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unread_Matrix_LaneIsolation_MentionsCrossRooms_WatermarkAdvances()
    {
        var projectId = await CreateProjectAsync();
        await CreateLaneAsync(projectId, "backend");
        await PostHumanAsync(projectId, "backend", "Dan", "lane talk");
        await PostHumanAsync(projectId, "backend", "Dan", "hall ping @guldan");

        var laneFeed = await UnreadAsync(projectId, "guldan", "backend");
        laneFeed.Items.Select(m => m.Text).ShouldBe(["lane talk", "hall ping @guldan"]);
        laneFeed.WatermarkAt.ShouldNotBe(default);

        var secondRead = await UnreadAsync(projectId, "guldan", "backend");
        secondRead.Items.Select(m => m.Text).ShouldBe(["hall ping @guldan"]);

        var otherAlias = await UnreadAsync(projectId, "marshal", "backend");
        otherAlias.Items.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Unread_MentionInForeignRoom_ShowsUpInOtherRoomFeeds()
    {
        var projectId = await CreateProjectAsync();
        await CreateLaneAsync(projectId, "backend");
        await PostHumanAsync(projectId, "backend", "Dan", "cross-room @guldan");

        var hallFeed = await UnreadAsync(projectId, "guldan", "all");

        hallFeed.Items.Select(m => m.Room).ShouldBe(["backend"]);
    }

    [Fact]
    public async Task Unread_WithoutAlias_Returns400()
    {
        var projectId = await CreateProjectAsync();

        var response = await _client.GetAsync($"/api/projects/{projectId}/wave/rooms/all/unread");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unread_UnknownLane_Returns404WithNamedCode()
    {
        var projectId = await CreateProjectAsync();

        var response = await _client.GetAsync($"/api/projects/{projectId}/wave/rooms/frontend/unread?alias=guldan");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemTitleAsync(response)).ShouldBe("MissingWaveRoom");
    }

    [Fact]
    public async Task Unread_AgentDoor_ResolvesAliasFromSession()
    {
        var projectId = await CreateProjectAsync();
        await PostHumanAsync(projectId, "all", "Marshal", "hello @Dan");

        var response = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/unread", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feed = await response.Content.ReadFromJsonAsync<FeedJson>(JsonOptions);
        feed!.Items.Select(m => m.AuthorAlias).ShouldBe(["Marshal"]);
    }

    [Fact]
    public async Task History_HumanDoor_ReturnsChronologicalPage()
    {
        var projectId = await CreateProjectAsync();
        await CreateLaneAsync(projectId, "backend");
        await PostHumanAsync(projectId, "backend", "Dan", "one");
        var second = await PostMessageToAsync(projectId, "backend", "two");
        await PostHumanAsync(projectId, "backend", "Dan", "three");

        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/wave/rooms/backend/messages?limit=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions);
        page!.Items.Select(m => m.Text).ShouldBe(["two", "three"]);

        var back = await _client.GetAsync(
            $"/api/projects/{projectId}/wave/rooms/backend/messages?before={Uri.EscapeDataString(second.ToString("O"))}");
        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        var older = await back.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions);
        older!.Items.Select(m => m.Text).ShouldBe(["one"]);
    }

    [Fact]
    public async Task History_BeforeCursor_PagesBackward()
    {
        var projectId = await CreateProjectAsync();
        var first = await PostMessageAsync(projectId, "one");
        await PostMessageAsync(projectId, "two");
        var third = await PostMessageAsync(projectId, "three");

        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/wave/rooms/all/messages?limit=2&before={Uri.EscapeDataString(third.ToString("O"))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions);
        page!.Items.Select(m => m.Text).ShouldBe(["one", "two"]);
        page.Items[0].CreatedAt.ShouldBe(first, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task History_AgentDoor_RequiresTheSessionHeader()
    {
        var projectId = await CreateProjectAsync();
        await PostHumanAsync(projectId, "all", "Dan", "agent visible");

        var anonymous = await _client.GetAsync(
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var response = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions);
        page!.Items.Select(m => m.Text).ShouldBe(["agent visible"]);
    }

    [Fact]
    public async Task History_UnknownLane_Returns404WithNamedCode()
    {
        var projectId = await CreateProjectAsync();

        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/wave/rooms/frontend/messages");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemTitleAsync(response)).ShouldBe("MissingWaveRoom");
    }

    [Fact]
    public async Task History_NeverAdvancesWatermarks()
    {
        var projectId = await CreateProjectAsync();
        await PostHumanAsync(projectId, "all", "Dan", "history one");
        var feed = await UnreadAsync(projectId, "guldan", "all");

        await _client.GetAsync($"/api/projects/{projectId}/wave/rooms/all/messages");
        await _client.GetAsync($"/api/projects/{projectId}/wave/rooms/all/messages");

        var secondRead = await UnreadAsync(projectId, "guldan", "all");
        secondRead.Items.ShouldBeEmpty();
        secondRead.WatermarkAt.ShouldBe(feed.WatermarkAt);
    }

    [Fact]
    public async Task Rooms_ReturnsAllPlusLanes_WithLastActivity()
    {
        var projectId = await CreateProjectAsync();
        await CreateLaneAsync(projectId, "backend");
        var posted = await PostHumanAsync(projectId, "backend", "Dan", "lane talk");
        var message = await posted.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);

        var response = await _client.GetFromJsonAsync<RoomsJson>(
            $"/api/projects/{projectId}/wave/rooms", JsonOptions);

        response!.Items.Select(r => r.Room).ShouldBe(["all", "backend"]);
        response.Items.Single(r => r.Room == "all").LastActivityAt.ShouldBeNull();

        var backendActivity = response.Items.Single(r => r.Room == "backend").LastActivityAt;
        backendActivity.ShouldNotBeNull();
        backendActivity.Value.ShouldBe(message!.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Rooms_UnknownProject_Returns404WithNamedCode()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/wave/rooms");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemTitleAsync(response)).ShouldBe("MissingProject");
    }

    private async Task<HttpResponseMessage> PostHumanAsync(
        Guid projectId, string room, string authorAlias, string text) =>
        await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/wave/rooms/{room}/messages",
            new { AuthorAlias = authorAlias, Text = text }, JsonOptions);

    private async Task<DateTime> PostMessageAsync(Guid projectId, string text)
    {
        return await PostMessageToAsync(projectId, "all", text);
    }

    private async Task<DateTime> PostMessageToAsync(Guid projectId, string room, string text)
    {
        var response = await PostHumanAsync(projectId, room, "Dan", text);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);
        return message!.CreatedAt;
    }

    private async Task<FeedJson> UnreadAsync(Guid projectId, string alias, string room)
    {
        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/wave/rooms/{room}/unread?alias={alias}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FeedJson>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendAgentAsync(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(SessionHeader.Name, AgentSessionId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<string> ProblemTitleAsync(HttpResponseMessage response)
    {
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return problem["title"]!.GetValue<string>();
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var name = $"wave-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private async Task CreateLaneAsync(Guid projectId, string name)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/lanes",
            new { Name = name, Contour = (string?)null, SerialOrder = (int?)null, Priority = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SeedAgentSessionAsync()
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{AgentSessionId.ToString().ToUpperInvariant()}', 'Dan', 'claude', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record MessageJson(
        Guid Id, string Room, string AuthorAlias, string Text, string[] Mentions, DateTime CreatedAt);

    private sealed record FeedJson(MessageJson[] Items, DateTime WatermarkAt);

    private sealed record HistoryJson(MessageJson[] Items);

    private sealed record RoomsJson(RoomJson[] Items);

    private sealed record RoomJson(string Room, DateTime? LastActivityAt);
}
