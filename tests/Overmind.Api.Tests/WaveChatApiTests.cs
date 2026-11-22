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
    private static readonly Guid MarshalSessionId = Guid.NewGuid();

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
        message.AuthorRole.ShouldBe("owner");
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
    public async Task Post_TextOverCap_StoresGraceBandWholeWithWarning()
    {
        // 266: over-cap wave text is accepted-with-warnings — 501..610 stores whole,
        // the response carries the frozen {accepted_len, limit, truncated, lost_tail_preview}.
        var projectId = await CreateProjectAsync();

        var response = await PostHumanAsync(projectId, "all", "Dan", new string('x', 501));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        payload["text"]!.GetValue<string>().Length.ShouldBe(501);
        var warning = payload["warnings"]!.AsArray().Single()!.AsObject();
        warning["accepted_len"]!.GetValue<int>().ShouldBe(501);
        warning["limit"]!.GetValue<int>().ShouldBe(500);
        warning["truncated"]!.GetValue<bool>().ShouldBeFalse();
        // JsonNode maps a JSON null to a C# null — the key is present with a null value
        warning.ContainsKey("lost_tail_preview").ShouldBeTrue();
        warning["lost_tail_preview"].ShouldBeNull();
    }

    [Fact]
    public async Task Post_AgentDoor_PastCeiling_TruncatesWithLostTailPreview()
    {
        // Owner worked example at the wave door: 611+ lands truncated to 600 with a preview
        // of the dropped tail; the stored row never exceeds the keep length.
        var projectId = await CreateProjectAsync();

        var response = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages", new { Text = new string('x', 614) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        payload["text"]!.GetValue<string>().Length.ShouldBe(600);
        var warning = payload["warnings"]!.AsArray().Single()!.AsObject();
        warning["accepted_len"]!.GetValue<int>().ShouldBe(600);
        warning["limit"]!.GetValue<int>().ShouldBe(500);
        warning["truncated"]!.GetValue<bool>().ShouldBeTrue();
        warning["lost_tail_preview"]!.GetValue<string>().Length.ShouldBe(14);

        var pastCeiling = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages", new { Text = new string('x', 611 + 2_000) });
        pastCeiling.StatusCode.ShouldBe(HttpStatusCode.OK);
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
        message.AuthorRole.ShouldBe("worker");
    }

    [Fact]
    public async Task History_SurfacesTheStoredAuthorRole()
    {
        var projectId = await CreateProjectAsync();
        await SeedMessageAsync(projectId, "all", "Marshal", "marshal", "wave wide");
        await PostHumanAsync(projectId, "all", "Dan", "from dan");

        var feed = await UnreadAsync(projectId, "marshal", "all");
        feed.Items.Select(m => (m.AuthorAlias, m.AuthorRole))
            .ShouldBe([("Marshal", "marshal"), ("Dan", "owner")]);

        var page = await _client.GetFromJsonAsync<HistoryJson>(
            $"/api/projects/{projectId}/wave/rooms/all/messages", JsonOptions);

        page!.Items.Select(m => (m.AuthorAlias, m.AuthorRole))
            .ShouldBe([("Marshal", "marshal"), ("Dan", "owner")]);
    }

    [Fact]
    public async Task Post_AgentDoor_MarshalSession_StampsTheMarshalRole()
    {
        var projectId = await CreateProjectAsync();

        var response = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages",
            new { Text = "holding the wave" }, MarshalSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);
        message!.AuthorAlias.ShouldBe("Boss");
        message.AuthorRole.ShouldBe("marshal");

        var feed = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/unread", null, MarshalSessionId);
        var page = await feed.Content.ReadFromJsonAsync<FeedJson>(JsonOptions);
        page!.Items.Select(m => (m.AuthorAlias, m.AuthorRole))
            .ShouldBe([("Boss", "marshal")]);
    }

    [Fact]
    public async Task AgentDoor_PercentEncodedRoomSegment_RoutesAsOneSegment_AndBindsTheDecodedName()
    {
        const string room = "core & docs";
        var projectId = await CreateProjectAsync();
        await SeedLegacyLaneAsync(projectId, room);
        var segment = Uri.EscapeDataString(room);

        var response = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/messages", new { Text = "encoded segment inbound" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);
        message!.Room.ShouldBe(room);
        message.Text.ShouldBe("encoded segment inbound");

        var peek = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/peek", null);
        peek.StatusCode.ShouldBe(HttpStatusCode.OK);
        var counts = await peek.Content.ReadFromJsonAsync<PeekJson>(JsonOptions);
        counts!.Room.ShouldBe(room);
        counts.Count.ShouldBe(1);

        var slashFeed = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/unread", null);
        slashFeed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feed = await slashFeed.Content.ReadFromJsonAsync<FeedJson>(JsonOptions);
        feed!.Items.Select(m => m.Room).ShouldBe([room]);

        var hallFeed = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/unread", null);
        var hall = await hallFeed.Content.ReadFromJsonAsync<FeedJson>(JsonOptions);
        hall!.Items.ShouldBeEmpty();
    }

    // Handler-edge normalization (spec 236 scope 1b): the bound {room} arrives percent-encoded, so
    // the slash-room only resolves because WaveChatApi unescapes it before the roster lookup.
    //
    // Read the harness before borrowing the claim from this file: ApiFactory is
    // WebApplicationFactory<Program> over the in-memory TestServer, not Kestrel. What this file can
    // and does measure is what the handler receives and what it answers; the production routing of
    // %2F is pinned by the recorded live-Host probe (see the task obstacles and audits/), and the
    // Kestrel-backed factory that would measure it in-suite is feature 239, not this one.
    [Fact]
    public async Task AgentDoor_PercentEncodedSlashRoom_UnescapesAtTheHandlerEdge_AndDeliversIntoThatRoom()
    {
        const string room = "C. Edge/Pi";
        var projectId = await CreateProjectAsync();
        await SeedLegacyLaneAsync(projectId, room);
        var segment = Uri.EscapeDataString(room);

        var response = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/messages", new { Text = "slash room inbound" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<MessageJson>(JsonOptions);
        message!.Room.ShouldBe(room);

        var peek = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/peek", null);
        peek.StatusCode.ShouldBe(HttpStatusCode.OK);
        var counts = await peek.Content.ReadFromJsonAsync<PeekJson>(JsonOptions);
        counts!.Room.ShouldBe(room);
        counts.Count.ShouldBe(1);

        var slashFeed = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/unread", null);
        slashFeed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feed = await slashFeed.Content.ReadFromJsonAsync<FeedJson>(JsonOptions);
        feed!.Items.Select(m => m.Room).ShouldBe([room]);

        var history = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/{segment}/messages", null);
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await history.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions);
        page!.Items.Select(m => m.Room).ShouldBe([room]);

        var hallFeed = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/unread", null);
        var hall = await hallFeed.Content.ReadFromJsonAsync<FeedJson>(JsonOptions);
        hall!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_AgentDoor_RawSlashRoom_NeverReachesTheWaveHandler()
    {
        const string room = "C. Edge/Pi";
        var projectId = await CreateProjectAsync();
        await SeedLegacyLaneAsync(projectId, room);

        var raw = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/{room}/messages", new { Text = "unencoded wire" });

        // This harness answers 405: with the slash unescaped the shape is no longer
        // `rooms/{room}/messages`, so the SPA fallback — GET/HEAD only — is what the leftover path
        // matches. The wave handler is reached by neither, which is the point: a MissingWaveRoom
        // problem body would mean it was. Measured against a real Kestrel host as well
        // (audits/236-t0-kestrel-probe.md): the raw wire answers 405 with an empty body there too,
        // so this holds on both hosts. The earlier "404, no route matched" wording was prose and
        // the probe retired it.
        raw.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (raw.Content.Headers.ContentType?.MediaType != "application/problem+json").ShouldBeTrue();
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
        secondRead.Items.ShouldBeEmpty();

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
    public async Task Peek_AgentDoor_ReturnsRoomCountAndIds_WithoutAdvancingTheWatermark()
    {
        var projectId = await CreateProjectAsync();
        await PostHumanAsync(projectId, "all", "Dan", "hello @Dan");
        await PostHumanAsync(projectId, "all", "Dan", "plain talk");

        var first = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/peek", null);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var peek = await first.Content.ReadFromJsonAsync<PeekJson>(JsonOptions);
        peek!.Room.ShouldBe("all");
        peek.Count.ShouldBe(2);
        peek.Ids.Length.ShouldBe(2);

        var second = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/all/peek", null);
        var repeat = await second.Content.ReadFromJsonAsync<PeekJson>(JsonOptions);
        repeat!.Count.ShouldBe(2);
        repeat.Ids.ShouldBe(peek.Ids);

        var feed = await UnreadAsync(projectId, "dan", "all");
        feed.Items.Select(m => m.Id).ShouldBe(peek.Ids);
        feed.Items.Select(m => m.Text).ShouldBe(["hello @Dan", "plain talk"]);
    }

    [Fact]
    public async Task Unread_AgentDoor_BindsSinceLimitAndReplayMentions()
    {
        var projectId = await CreateProjectAsync();
        await CreateLaneAsync(projectId, "backend");
        await PostHumanAsync(projectId, "all", "Dan", "first");
        await PostHumanAsync(projectId, "all", "Dan", "second");

        var capped = await AgentUnreadAsync(projectId, "all", limit: 1);
        capped.Items.Select(m => m.Text).ShouldBe(["second"]);

        var future = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(2).ToString("O"));
        var narrowed = await AgentUnreadAsync(projectId, "all", since: future);
        narrowed.Items.ShouldBeEmpty();

        await SeedMessageAsync(projectId, "backend", "Dan", "worker", "ghost ping");
        var replayed = await AgentUnreadAsync(projectId, "all", since: future, replayMentions: true);
        replayed.Items.Select(m => m.Text).ShouldBe(["ghost ping"]);
    }

    [Fact]
    public async Task Peek_UnknownLane_Returns404WithNamedCode()
    {
        var projectId = await CreateProjectAsync();

        var response = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/frontend/peek", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemTitleAsync(response)).ShouldBe("MissingWaveRoom");
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

    private async Task<FeedJson> AgentUnreadAsync(
        Guid projectId, string room, string? since = null, int? limit = null, bool replayMentions = false)
    {
        var query = new List<string>();
        if (since is not null)
        {
            query.Add($"since={since}");
        }

        if (limit is not null)
        {
            query.Add($"limit={limit}");
        }

        if (replayMentions)
        {
            query.Add("replayMentions=true");
        }

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
        var response = await SendAgentAsync(HttpMethod.Get,
            $"/api/agent/projects/{projectId}/wave/rooms/{room}/unread{suffix}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FeedJson>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendAgentAsync(
        HttpMethod method, string url, object? body, Guid? sessionId = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(SessionHeader.Name, (sessionId ?? AgentSessionId).ToString());
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

    private async Task<string> ProblemDetailAsync(HttpResponseMessage response)
    {
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return problem["detail"]!.GetValue<string>();
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
        var slug = $"001-{name}-work";
        var create = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = "Lane work" }, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bind = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { Lane = name }, JsonOptions);
        bind.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SeedAgentSessionAsync()
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{AgentSessionId.ToString().ToUpperInvariant()}', 'Dan', 'claude', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, Role, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{MarshalSessionId.ToString().ToUpperInvariant()}', 'Boss', 'claude', 'marshal', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
    }

    private async Task SeedLegacyLaneAsync(Guid projectId, string name) =>
        await ExecuteAsync($"""
            INSERT INTO PlanningLanes (Id, ProjectId, Name, CreatedAt, UpdatedAt)
            VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{projectId.ToString().ToUpperInvariant()}', '{name}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')
            """);

    private async Task SeedMessageAsync(
        Guid projectId, string room, string authorAlias, string authorRole, string text)
    {
        await ExecuteAsync($"""
            INSERT INTO WaveMessages (Id, ProjectId, Room, AuthorAlias, AuthorRole, Text, Mentions, CreatedAt)
            VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{projectId.ToString().ToUpperInvariant()}', '{room}', '{authorAlias}', '{authorRole}', '{text}', ' {authorAlias.ToLowerInvariant()} ', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')
            """);
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record MessageJson(
        Guid Id, string Room, string AuthorAlias, string Text, string[] Mentions, DateTime CreatedAt,
        string AuthorRole);

    private sealed record FeedJson(MessageJson[] Items, DateTime WatermarkAt);

    private sealed record PeekJson(string Room, int Count, Guid[] Ids);

    private sealed record HistoryJson(MessageJson[] Items);

    private sealed record RoomsJson(RoomJson[] Items);

    private sealed record RoomJson(string Room, DateTime? LastActivityAt);
}
