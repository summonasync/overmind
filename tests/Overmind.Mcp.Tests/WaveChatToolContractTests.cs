using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class WaveChatToolContractTests : IAsyncLifetime
{
    private const string SelfAlias = "dan";
    private const string AuthorAlias = "kai";

    private static readonly DateTime Base = new(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);

    private static readonly string RoomsRoute =
        $"/api/agent/projects/{Canned.ProjectId}/wave/rooms";

    private StubOvermindApi _stub = null!;
    private OvermindApiClient _api = null!;
    private WaveChatFake _fake = null!;

    public Task InitializeAsync()
    {
        _stub = StubOvermindApi.Start();
        _api = new OvermindApiClient(OvermindApiClient.CreateHttpClient(_stub.BaseUrl));
        _fake = new WaveChatFake();

        _stub.Route("GET", RoomsRoute, _ => new StubOvermindApi.Response(200, _fake.RoomsListingJson()));
        _stub.Route("GET", $"{RoomsRoute}/([^/]+)/unread",
            req => new StubOvermindApi.Response(200, _fake.UnreadJson(RoomOf(req.Path))));
        _stub.Route("GET", $"{RoomsRoute}/([^/]+)/peek",
            req => new StubOvermindApi.Response(200, _fake.PeekJson(RoomOf(req.Path))));
        _stub.Route("POST", $"{RoomsRoute}/([^/]+)/messages",
            req => new StubOvermindApi.Response(200, _fake.PostedJson(RoomOf(req.Path), TextOf(req.Body))));

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _stub.DisposeAsync();
    }

    [Fact]
    public async Task MissingProjectId_ResolvesThroughTheOnboardCwd()
    {
        _stub.Route("POST", "/api/onboard", 200, Canned.OnboardJson);

        var posted = await Tool().PostMessage("all", "wave opens in ten");

        Text(posted).ShouldContain("posted to 'all'");

        var onboard = _stub.Requests.Single(r => r.Method == "POST" && r.Path == "/api/onboard");
        var body = JsonNode.Parse(onboard.Body)!;
        body["cwd"]!.GetValue<string>().ShouldBe(Environment.CurrentDirectory);
        body["create"]!.GetValue<bool>().ShouldBeFalse();

        _stub.Requests.ShouldContain(r => r.Path == $"{RoomsRoute}/all/messages");
    }

    [Fact]
    public async Task UnresolvableCwd_ReturnsTheGuidanceWithoutPosting()
    {
        _stub.Route("POST", "/api/onboard", 404, Canned.OnboardUnregisteredCwdJson);

        var ex = await Should.ThrowAsync<McpException>(() => Tool().PostMessage("all", "hello"));

        ex.Message.ShouldContain("Could not resolve the project for the working directory");
        ex.Message.ShouldContain("Pass projectId explicitly");
        _stub.Requests.ShouldNotContain(r => r.Path.Contains("/wave/rooms"));
    }

    [Fact]
    public async Task ExplicitProjectId_SkipsTheOnboardRoundTrip()
    {
        await Tool().PostMessage("all", "hello", Canned.ProjectId);

        _stub.Requests.ShouldNotContain(r => r.Path == "/api/onboard");
    }

    [Fact]
    public async Task PostMessage_PostsTheText_AndReportsIt()
    {
        var posted = await Tool().PostMessage("lane-a", $"@{SelfAlias} ping from the marshal", Canned.ProjectId);

        var request = _stub.Requests.Single(r => r.Method == "POST");
        request.Path.ShouldBe($"{RoomsRoute}/lane-a/messages");
        request.Body.ShouldContain($"\"text\":\"@{SelfAlias} ping from the marshal\"");
        Text(posted).ShouldContain("posted to 'lane-a'");
        Text(posted).ShouldContain($"as {AuthorAlias}");
        Text(posted).ShouldContain($"Mentions: {SelfAlias}");
    }

    [Fact]
    public async Task PostMessage_UnknownRoom_SurfacesTheNamedError()
    {
        _stub.Route("POST", $"{RoomsRoute}/([^/]+)/messages", 404,
            """{"title":"MissingWaveRoom","detail":"Wave room 'ghost' does not exist in this project: rooms are the lane roster plus the shared 'all' room. Rooms live now: 'all', 'lane-a'."}""");

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().PostMessage("ghost", "hello", Canned.ProjectId));

        ex.Message.ShouldContain("Wave room 'ghost' does not exist");
        ex.Message.ShouldContain("Rooms live now: 'all', 'lane-a'");
    }

    [Fact]
    public async Task PostMessage_TextTooLong_SurfacesTheNamedError()
    {
        _stub.Route("POST", $"{RoomsRoute}/([^/]+)/messages", 400,
            """{"title":"WaveTextTooLong","detail":"Wave message text is 512 characters; the cap is 500."}""");

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().PostMessage("all", "flood", Canned.ProjectId));

        ex.Message.ShouldContain("the cap is 500");
    }

    [Fact]
    public async Task Unread_LaneIsolation_KeepsOtherLanesOutOfTheFeed()
    {
        _fake.Post("lane-a", "shared file inbound, coordinate with me");
        _fake.Post("lane-b", "lane-b blocker: migration stuck");

        var feed = await Feed("lane-a");

        Rooms(feed).ShouldBe(new[] { "lane-a" });
        Texts(feed).ShouldBe(new[] { "shared file inbound, coordinate with me" });
    }

    [Fact]
    public async Task Unread_MentionCrossesRooms()
    {
        _fake.Post("lane-b", $"@{SelfAlias} context for the pin you asked about");

        var feed = await Feed("lane-a");

        Texts(feed).ShouldBe(new[] { $"@{SelfAlias} context for the pin you asked about" });
    }

    [Fact]
    public async Task Unread_WatermarkAdvancesOnRead()
    {
        _fake.Post("all", "wave opens, read the board");
        _fake.Post("all", "board moved, task 3 unblocked");

        var first = await Feed(null);
        Texts(first).Length.ShouldBe(2);
        first["watermarkAt"]!.GetValue<string>().ShouldBe(Stamp(Base.AddMinutes(1)));

        var second = await Feed(null);
        Texts(second).ShouldBeEmpty();
        second["watermarkAt"]!.GetValue<string>().ShouldBe(Stamp(Base.AddMinutes(1)));
    }

    [Fact]
    public async Task Unread_FanOutDeduplicatesMentionAcrossRooms()
    {
        _fake.Post("lane-a", "standup notes");
        _fake.Post("lane-b", $"@{SelfAlias} handoff: lane-b is green, moving to review");

        var feed = await Feed("lane-a,lane-b");

        Texts(feed).Length.ShouldBe(2);
        Texts(feed).Count(t => t.Contains($"@{SelfAlias}")).ShouldBe(1);
    }

    [Fact]
    public async Task Unread_RoomsAreReadOldestActivityFirst()
    {
        _fake.Post("lane-idle", "signal that settled earlier");
        _fake.Post("lane-hot", "signal still moving");

        await Feed(null);

        var reads = _stub.Requests
            .Where(r => r.Path.EndsWith("/unread", StringComparison.Ordinal))
            .Select(r => RoomOf(r.Path))
            .ToArray();
        reads.ShouldBe(new[] { "lane-idle", "lane-hot" });
    }

    [Fact]
    public async Task Unread_SkipsRoomsWithoutActivity()
    {
        _fake.Quiet("lane-quiet");
        _fake.Post("lane-live", "only the live room carries signal");

        var feed = await Feed(null);

        Rooms(feed).ShouldBe(new[] { "lane-live" });
        _stub.Requests.ShouldNotContain(r => r.Path.Contains("lane-quiet"));
    }

    [Fact]
    public async Task Unread_BlankRoomsFilter_IsRefusedWithoutReading()
    {
        var ex = await Should.ThrowAsync<McpException>(() => Tool().FetchUnread(" ,", projectId: Canned.ProjectId));

        ex.Message.ShouldContain("carries no room names");
        _stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Peek_MergesPerRoomCounts_WithoutAdvancingWatermarks()
    {
        _fake.Post("lane-a", "standup notes");
        _fake.Post("lane-b", $"@{SelfAlias} handoff: lane-b is green, moving to review");
        _fake.Post("lane-a", "board moved again");

        var first = await Peek("lane-a,lane-b");
        var second = await Peek("lane-a,lane-b");

        Rooms(first).ShouldBe(new[] { "lane-a", "lane-b" });
        var peeks = first["peeks"]!.AsArray().Select(p => p!.AsObject()).ToArray();
        peeks.Select(p => p["room"]!.GetValue<string>())
            .ShouldBe(new[] { "lane-a", "lane-b" });
        peeks.Select(p => p["count"]!.GetValue<int>()).ShouldBe(new[] { 3, 1 });
        var ids = peeks.SelectMany(p => p["ids"]!.AsArray()).Select(i => i!.GetValue<string>()).ToArray();
        ids.Length.ShouldBe(4);
        ids.Distinct().Count().ShouldBe(3);

        second.ToJsonString().ShouldBe(first.ToJsonString());
        _stub.Requests.ShouldNotContain(r => r.Path.EndsWith("/unread", StringComparison.Ordinal));
        _stub.Requests.Count(r => r.Path.EndsWith("/peek", StringComparison.Ordinal)).ShouldBe(4);
    }

    [Fact]
    public async Task Peek_UnknownRoom_SurfacesTheNamedError()
    {
        _stub.Route("GET", $"{RoomsRoute}/([^/]+)/peek", 404,
            """{"title":"MissingWaveRoom","detail":"Wave room 'ghost' does not exist in this project: rooms are the lane roster plus the shared 'all' room. Rooms live now: 'all', 'lane-a'."}""");

        var ex = await Should.ThrowAsync<McpException>(
            () => Tool().PeekRooms("ghost", Canned.ProjectId));

        ex.Message.ShouldContain("Wave room 'ghost' does not exist");
        ex.Message.ShouldContain("Rooms live now: 'all', 'lane-a'");
    }

    [Fact]
    public async Task Unread_CompactMode_ProjectsMessagesToTheShortShape()
    {
        var longText = new string('x', 250);
        _fake.Post("lane-a", longText);
        _fake.Post("lane-b", longText);

        var full = await Feed("lane-a");
        Texts(full).ShouldBe([longText]);

        var compactFeed = await Tool().FetchUnread("lane-b", compact: true, projectId: Canned.ProjectId);
        var compact = JsonNode.Parse(compactFeed.GetRawText())!
            ["messages"]!.AsArray().Select(n => n!.AsObject()).Single();

        compact.Select(kv => kv.Key).ShouldBe(["id", "room", "authorAlias", "createdAt", "text"]);
        compact["text"]!.GetValue<string>().ShouldBe($"{new string('x', 200)}…");
        compact["room"]!.GetValue<string>().ShouldBe("lane-b");
        compact["authorAlias"]!.GetValue<string>().ShouldBe(AuthorAlias);
    }

    [Fact]
    public async Task Unread_PassesSinceLimitAndReplayMentionsToTheHost()
    {
        _fake.Post("lane-a", "traffic for the query probe");

        await Tool().FetchUnread("lane-a",
            since: Base.AddMinutes(-5), limit: 25, replayMentions: true, projectId: Canned.ProjectId);

        var parameterized = _stub.Requests.Single(r => r.Path.EndsWith("/unread", StringComparison.Ordinal));
        parameterized.Query.ShouldContain(Uri.EscapeDataString(Base.AddMinutes(-5).ToString("O")));
        parameterized.Query.ShouldContain("limit=25");
        parameterized.Query.ShouldContain("replayMentions=true");

        await Tool().FetchUnread("lane-a", projectId: Canned.ProjectId);

        var bare = _stub.Requests.Last(r => r.Path.EndsWith("/unread", StringComparison.Ordinal));
        bare.Query.ShouldBeEmpty();
    }

    private async Task<JsonObject> Feed(string? rooms)
    {
        var element = await Tool().FetchUnread(rooms, projectId: Canned.ProjectId);
        return JsonNode.Parse(element.GetRawText())!.AsObject();
    }

    private async Task<JsonObject> Peek(string? rooms)
    {
        var element = await Tool().PeekRooms(rooms, Canned.ProjectId);
        return JsonNode.Parse(element.GetRawText())!.AsObject();
    }

    private static string[] Texts(JsonObject feed) =>
        feed["messages"]!.AsArray().Select(m => m!["text"]!.GetValue<string>()).ToArray();

    private static string[] Rooms(JsonObject feed) =>
        feed["rooms"]!.AsArray().Select(r => r!.GetValue<string>()).ToArray();

    private static string Stamp(DateTime at) => at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static string RoomOf(string path) =>
        path.Split("/wave/rooms/")[1].Split('/')[0];

    private static string TextOf(string body) =>
        JsonNode.Parse(body)!["text"]!.GetValue<string>();

    private static string Text(CallToolResult result) =>
        ((TextContentBlock)result.Content[0]).Text;

    private WaveChatTools Tool() => new(_api, new MutationGuard(_api), new ProjectResolver(_api));

    private sealed class WaveChatFake
    {
        private const string WatermarkFloor = "0001-01-01T00:00:00Z";

        private readonly List<string> _rooms = [];
        private readonly List<JsonObject> _messages = [];
        private DateTime _watermark = DateTime.MinValue;
        private int _ticks;

        public void Quiet(string room)
        {
            if (!_rooms.Contains(room))
            {
                _rooms.Add(room);
            }
        }

        public void Post(string room, string text)
        {
            var mentions = text.Contains($"@{SelfAlias}", StringComparison.Ordinal)
                ? new[] { SelfAlias }
                : Array.Empty<string>();
            _messages.Add(new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["room"] = room,
                ["authorAlias"] = AuthorAlias,
                ["text"] = text,
                ["mentions"] = new JsonArray(mentions.Select(m => (JsonNode?)m).ToArray()),
                ["createdAt"] = Base.AddMinutes(_ticks++)
            });

            Quiet(room);
        }

        public string PostedJson(string room, string text)
        {
            Post(room, text);
            return _messages[^1].ToJsonString();
        }

        public string RoomsListingJson()
        {
            var items = new JsonArray();
            foreach (var room in Enumerable.Reverse(_rooms))
            {
                var activity = _messages
                    .Where(m => Room(m) == room)
                    .Select(At)
                    .DefaultIfEmpty(DateTime.MinValue);
                items.Add(new JsonObject
                {
                    ["room"] = room,
                    ["lastActivityAt"] = activity.Max() == DateTime.MinValue ? null : activity.Max()
                });
            }

            return new JsonObject { ["items"] = items }.ToJsonString();
        }

        public string PeekJson(string room)
        {
            var ids = _messages
                .Where(m => (Room(m) == room && At(m) > _watermark) || Mentions(m).Contains(SelfAlias))
                .OrderBy(At)
                .ThenBy(Id)
                .Select(Id)
                .ToArray();

            return new JsonObject
            {
                ["room"] = room,
                ["count"] = ids.Length,
                ["ids"] = new JsonArray(ids.Select(id => (JsonNode?)id).ToArray())
            }.ToJsonString();
        }

        public string UnreadJson(string room)
        {
            var matched = _messages
                .Where(m => (Room(m) == room && At(m) > _watermark) || Mentions(m).Contains(SelfAlias))
                .OrderBy(At)
                .ThenBy(Id)
                .ToList();
            if (matched.Count > 0)
            {
                _watermark = At(matched[^1]);
            }

            var items = new JsonArray();
            foreach (var message in matched)
            {
                items.Add(message.DeepClone());
            }

            return new JsonObject
            {
                ["items"] = items,
                ["watermarkAt"] = _watermark == DateTime.MinValue ? WatermarkFloor : Stamp(_watermark)
            }.ToJsonString();
        }

        private static string Stamp(DateTime at) => at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

        private static string Room(JsonObject message) => message["room"]!.GetValue<string>();

        private static DateTime At(JsonObject message) => message["createdAt"]!.GetValue<DateTime>();

        private static string Id(JsonObject message) => message["id"]!.GetValue<string>();

        private static string[] Mentions(JsonObject message) =>
            message["mentions"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
    }
}
