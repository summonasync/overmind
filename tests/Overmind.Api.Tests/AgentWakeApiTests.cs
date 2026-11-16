using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentWakeApiTests : IDisposable
{
    private static readonly Guid AgentSessionId = Guid.NewGuid();

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentWakeApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-wake-api-{Guid.NewGuid():N}.db");
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
    public async Task Wake_WithoutTheSessionHeader_Returns400()
    {
        var response = await _client.GetAsync("/api/agent/wake");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemTitleAsync(response)).ShouldBe("Bad Request");
    }

    [Fact]
    public async Task Wake_WithAnUnknownSession_Returns400()
    {
        var response = await SendAgentAsync(HttpMethod.Get, "/api/agent/wake", null, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wake_ReturnsReplySignalsOnlyForTheSessionAlias()
    {
        var longReply = new string('y', 250);
        var mine = await SeedNotificationAsync("Dan", "approve the gate", longReply, MinutesAgo(10));
        await SeedNotificationAsync("Dan", "fyi only", null, MinutesAgo(9));
        await SeedNotificationAsync("Guldan", "someone else", "his reply", MinutesAgo(5));

        var wake = await WakeAsync(Uri.EscapeDataString(MinutesAgo(60).ToString("O")));

        wake.Signals.Select(s => s.RefId).ShouldBe([mine]);
        var signal = wake.Signals.Single();
        signal.Kind.ShouldBe("notification-reply");
        signal.RoomId.ShouldBeNull();
        signal.From.ShouldBe("human");
        signal.Excerpt.Length.ShouldBe(200);
    }

    [Fact]
    public async Task Wake_MatchesMentionsAcrossRooms_AndLeavesWatermarksUntouched()
    {
        var projectId = await CreateProjectAsync();
        await CreateLaneAsync(projectId, "backend");
        await PostHumanAsync(projectId, "all", "Dan", "hall ping @Dan");
        await PostHumanAsync(projectId, "all", "Dan", "plain talk");

        var consumed = await UnreadAsync(projectId, "dan", "all");
        consumed.Items.Select(m => m.Text).ShouldBe(["hall ping @Dan", "plain talk"]);

        await PostHumanAsync(projectId, "backend", "Dan", "lane ping @Dan");
        await PostHumanAsync(projectId, "all", "Dan", "fresh ping @Dan");

        var wake = await WakeAsync(Uri.EscapeDataString(MinutesAgo(60).ToString("O")));

        wake.Signals.Select(s => (s.Kind, s.RoomId, s.Excerpt)).ShouldBe(
        [
            ("wave-mention", "all", "hall ping @Dan"),
            ("wave-mention", "backend", "lane ping @Dan"),
            ("wave-mention", "all", "fresh ping @Dan"),
        ]);
        wake.Signals.Select(s => s.From).ShouldBe(["Dan", "Dan", "Dan"]);

        var allAfter = await UnreadAsync(projectId, "dan", "all");
        allAfter.Items.Select(m => m.Text).ShouldBe(["lane ping @Dan", "fresh ping @Dan"]);
    }

    [Fact]
    public async Task Wake_WithoutSince_DefaultsRepliesToTheLastSevenDays()
    {
        await SeedNotificationAsync("Dan", "ancient", "old reply", UtcNow.AddDays(-8));
        var recent = await SeedNotificationAsync("Dan", "current", "fresh reply", UtcNow.AddDays(-1));

        var wake = await WakeAsync();

        wake.Signals.Select(s => s.RefId).ShouldBe([recent]);
    }

    [Fact]
    public async Task Wake_DefaultWindow_DropsReadReplies_AndSinceReplayReturnsThem()
    {
        var pending = await SeedNotificationAsync("Dan", "open gate", "pending reply", MinutesAgo(10));
        var consumed = await SeedNotificationAsync(
            "Dan", "closed gate", "consumed reply", MinutesAgo(20), readAt: MinutesAgo(5));

        var wake = await WakeAsync();
        wake.Signals.Select(s => s.RefId).ShouldBe([pending]);

        var replay = await WakeAsync(Uri.EscapeDataString(MinutesAgo(60).ToString("O")));
        replay.Signals.Select(s => s.RefId).ShouldBe([consumed, pending]);
    }

    [Fact]
    public async Task Wake_AfterMarkRead_TheSignalLeavesTheDefaultWindow()
    {
        var pending = await SeedNotificationAsync("Dan", "gate", "approved", MinutesAgo(10));

        var before = await WakeAsync();
        before.Signals.Select(s => s.RefId).ShouldBe([pending]);

        var read = await _client.PostAsync($"/api/notifications/{pending}/read", null);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await WakeAsync();
        after.Signals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Wake_ProjectIdParameter_ScopesReplies_KeepsMachineWideWithoutIt()
    {
        var home = await CreateProjectAsync();
        var foreign = await CreateProjectAsync();
        var mine = await SeedNotificationAsync(
            "Dan", "home gate", "home reply", MinutesAgo(10), readAt: null, projectId: home);
        await SeedNotificationAsync(
            "Dan", "foreign gate", "foreign reply", MinutesAgo(8), readAt: null, projectId: foreign);
        await SeedNotificationAsync("Dan", "legacy gate", "legacy reply", MinutesAgo(6));

        var scoped = await WakeAsync(projectId: home);
        scoped.Signals.Select(s => s.RefId).ShouldBe([mine]);

        var machineWide = await WakeAsync();
        machineWide.Signals.Length.ShouldBe(3);
    }

    [Fact]
    public async Task Wake_WithoutSince_DefaultsMentionsToAfterTheSessionLastSeenAt()
    {
        var projectId = await CreateProjectAsync();
        await SeedMentionAsync(projectId, "all", "Dan", "worker", "stale ping @Dan", UtcNow.AddHours(-2));
        var live = await SeedMentionAsync(projectId, "all", "Dan", "worker", "live ping @Dan", UtcNow.AddMinutes(-30));

        var end = await _client.PostAsync($"/api/agent/sessions/{AgentSessionId}/end", null);
        end.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await SetSessionLastSeenAsync(UtcNow.AddHours(-1));

        var wake = await WakeAsync();

        wake.Signals.Select(s => s.RefId).ShouldBe([live]);
        CursorAt(wake.Cursor).ShouldBe(wake.Signals.Single().CreatedAt, TimeSpan.FromSeconds(1));
        CursorRefId(wake.Cursor).ShouldBe(wake.Signals.Single().RefId);
    }

    [Fact]
    public async Task Wake_WithoutSince_DefaultsMentionsToThePreTouchLastSeenForALiveSession()
    {
        var projectId = await CreateProjectAsync();
        await SetSessionLastSeenAsync(UtcNow.AddHours(-1));
        var live = await SeedMentionAsync(projectId, "all", "Dan", "worker", "fresh ping @Dan", UtcNow);

        var wake = await WakeAsync();

        wake.Signals.Select(s => s.RefId).ShouldBe([live]);
    }

    [Fact]
    public async Task Wake_HonorsSince_ForBothKinds()
    {
        await SeedNotificationAsync("Dan", "old gate", "old reply", UtcNow.AddDays(-2));
        var reply = await SeedNotificationAsync("Dan", "hot gate", "hot reply", UtcNow.AddHours(-2));
        var projectId = await CreateProjectAsync();
        await SeedMentionAsync(projectId, "all", "Dan", "worker", "older ping @Dan", UtcNow.AddHours(-6));
        var mention = await SeedMentionAsync(projectId, "all", "Dan", "worker", "recent ping @Dan", UtcNow.AddMinutes(-30));

        var windowed = await WakeAsync(Uri.EscapeDataString(UtcNow.AddHours(-5).ToString("O")));

        windowed.Signals.Select(s => (s.Kind, s.RefId)).ShouldBe(
        [
            ("notification-reply", reply),
            ("wave-mention", mention),
        ]);
        CursorAt(windowed.Cursor).ShouldBe(UtcNow.AddMinutes(-30), TimeSpan.FromSeconds(1));
        CursorRefId(windowed.Cursor).ShouldBe(mention);

        var drained = await WakeAsync(Uri.EscapeDataString(UtcNow.AddMinutes(5).ToString("O")));
        drained.Signals.ShouldBeEmpty();
        CursorAt(drained.Cursor).ShouldBe(UtcNow.AddMinutes(5), TimeSpan.FromSeconds(1));
        CursorRefId(drained.Cursor).ShouldBeNull();
    }

    [Fact]
    public async Task Wake_CapsThePageAt100_AndPagesWithTheCursor()
    {
        var projectId = await CreateProjectAsync();
        var now = UtcNow;
        var baseAt = new DateTime(
            now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc).AddHours(-2);
        for (var i = 0; i < 120; i++)
        {
            await SeedMentionAsync(
                projectId, "all", "Dan", "worker", $"ping {i} @Dan", baseAt.AddSeconds(i));
        }

        var firstPage = await WakeAsync(Uri.EscapeDataString(UtcNow.AddDays(-1).ToString("O")));

        firstPage.Signals.Length.ShouldBe(100);
        firstPage.Signals.Select(s => s.CreatedAt).ShouldBeInOrder(SortDirection.Ascending);
        CursorAt(firstPage.Cursor).ShouldBe(firstPage.Signals[99].CreatedAt, TimeSpan.FromMilliseconds(1));
        CursorRefId(firstPage.Cursor).ShouldBe(firstPage.Signals[99].RefId);

        var secondPage = await WakeAsync(Uri.EscapeDataString(firstPage.Cursor));

        secondPage.Signals.Length.ShouldBe(20);
        secondPage.Signals.Select(s => s.RefId)
            .Intersect(firstPage.Signals.Select(s => s.RefId))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Wake_TieGroupAcrossThePageBoundary_PagesWithoutLossOrDuplicates()
    {
        var projectId = await CreateProjectAsync();
        var now = UtcNow;
        var baseAt = new DateTime(
            now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc).AddHours(-2);
        var seeded = new List<Guid>();
        for (var i = 0; i < 105; i++)
        {
            seeded.Add(await SeedMentionAsync(projectId, "all", "Dan", "worker", $"tie ping {i} @Dan", baseAt));
        }

        var firstPage = await WakeAsync(Uri.EscapeDataString(UtcNow.AddDays(-1).ToString("O")));

        firstPage.Signals.Length.ShouldBe(100);
        firstPage.Signals.Select(s => s.CreatedAt).ShouldAllBe(at => at == baseAt);
        CursorAt(firstPage.Cursor).ShouldBe(baseAt, TimeSpan.FromMilliseconds(1));
        CursorRefId(firstPage.Cursor).ShouldBe(firstPage.Signals[99].RefId);

        var secondPage = await WakeAsync(Uri.EscapeDataString(firstPage.Cursor));

        secondPage.Signals.Length.ShouldBe(5);
        secondPage.Signals.Select(s => s.CreatedAt).ShouldAllBe(at => at == baseAt);
        var all = firstPage.Signals.Concat(secondPage.Signals).Select(s => s.RefId).ToList();
        all.Distinct().Count().ShouldBe(105);
        all.OrderBy(id => id).ShouldBe(seeded.OrderBy(id => id));
    }

    [Fact]
    public async Task Wake_TruncatesExcerptWithoutSplittingSurrogatePairs()
    {
        var text = new string('y', 199) + "😀" + new string('z', 60);
        await SeedNotificationAsync("Dan", "emoji reply", text, MinutesAgo(10));

        var wake = await WakeAsync(Uri.EscapeDataString(MinutesAgo(60).ToString("O")));

        wake.Signals.Single().Excerpt.ShouldBe(new string('y', 199) + "😀");
    }

    [Fact]
    public void Wake_DescriptionDocumentsTheSignalDefaults()
    {
        var endpoint = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/agent/wake");

        var description = endpoint.Metadata.OfType<IEndpointDescriptionMetadata>().Single().Description;

        description.ShouldContain("side-effect free");
        description.ShouldContain("readAt (consumed)");
        description.ShouldContain("the last 7 days");
        description.ShouldContain("OVERMIND_WAKE_REPLY_LOOKBACK_DAYS");
        description.ShouldContain("clamp");
        description.ShouldContain("machine-wide");
        description.ShouldContain("LastSeenAt");
        description.ShouldContain("echo it verbatim as since");
    }

    private DateTime UtcNow => DateTime.UtcNow;

    private DateTime MinutesAgo(int minutes) => DateTime.UtcNow.AddMinutes(-minutes);

    private async Task<WakeJson> WakeAsync(string? encodedSince = null, Guid? projectId = null)
    {
        var query = new List<string>();
        if (encodedSince is not null)
        {
            query.Add($"since={encodedSince}");
        }

        if (projectId is { } scoped)
        {
            query.Add($"projectId={scoped}");
        }

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
        var response = await SendAgentAsync(HttpMethod.Get, $"/api/agent/wake{suffix}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<WakeJson>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> PostHumanAsync(
        Guid projectId, string room, string authorAlias, string text) =>
        await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/wave/rooms/{room}/messages",
            new { AuthorAlias = authorAlias, Text = text }, JsonOptions);

    private async Task<FeedJson> UnreadAsync(Guid projectId, string alias, string room)
    {
        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/wave/rooms/{room}/unread?alias={alias}");
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

    private async Task<Guid> CreateProjectAsync()
    {
        var name = $"wake-{Guid.NewGuid():N}";
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
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, Role, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{AgentSessionId.ToString().ToUpperInvariant()}', 'Dan', 'claude', 'worker', '{Timestamp(DateTime.UtcNow)}', '{Timestamp(DateTime.UtcNow)}', NULL)
            """);
    }

    private async Task SetSessionLastSeenAsync(DateTime at)
    {
        await ExecuteAsync(
            $"UPDATE AgentSessions SET LastSeenAt = '{Timestamp(at)}' WHERE Id = '{AgentSessionId.ToString().ToUpperInvariant()}'");
    }

    private async Task<Guid> SeedNotificationAsync(
        string agent, string text, string? replyText, DateTime repliedAt,
        DateTime? readAt = null, Guid? projectId = null)
    {
        var id = Guid.NewGuid();
        var replied = replyText is null ? "NULL" : $"'{replyText}'";
        var read = readAt is null ? "NULL" : $"'{Timestamp(readAt.Value)}'";
        var project = projectId is null ? "NULL" : $"'{projectId.Value.ToString().ToUpperInvariant()}'";
        await ExecuteAsync($"""
            INSERT INTO AgentNotifications (Id, ProjectId, Kind, FeatureSlug, Tab, Text, Agent, CreatedAt, ReadAt, ReplyText, RepliedAt, HiddenAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', {project}, 1, NULL, NULL, '{text}', '{agent}', '{Timestamp(repliedAt)}', {read}, {replied}, '{Timestamp(repliedAt)}', NULL)
            """);
        return id;
    }

    private async Task<Guid> SeedMentionAsync(
        Guid projectId, string room, string authorAlias, string authorRole, string text, DateTime createdAt)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync($"""
            INSERT INTO WaveMessages (Id, ProjectId, Room, AuthorAlias, AuthorRole, Text, Mentions, CreatedAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', '{projectId.ToString().ToUpperInvariant()}', '{room}', '{authorAlias}', '{authorRole}', '{text}', ' {authorAlias.ToLowerInvariant()} ', '{Timestamp(createdAt)}')
            """);
        return id;
    }

    private static string Timestamp(DateTime at) => at.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF");

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record WakeSignalJson(
        string Kind, Guid RefId, string? RoomId, string From, DateTime CreatedAt, string Excerpt);

    private sealed record WakeJson(WakeSignalJson[] Signals, string Cursor);

    private static DateTime CursorAt(string cursor) =>
        DateTime.Parse(cursor.Split('|')[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static Guid? CursorRefId(string cursor) =>
        cursor.Split('|') is { Length: 2 } parts ? Guid.Parse(parts[1]) : null;

    private sealed record MessageJson(
        Guid Id, string Room, string AuthorAlias, string Text, string[] Mentions, DateTime CreatedAt,
        string AuthorRole);

    private sealed record FeedJson(MessageJson[] Items, DateTime WatermarkAt);
}
