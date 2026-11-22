using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Overmind.Core;
using Overmind.Host.Api;
using Overmind.Infrastructure.Notifications;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class NotificationsRepliesCursorApiTests : IDisposable
{
    private static readonly Guid AgentSessionId = Guid.NewGuid();
    private static readonly DateTime Basis = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationsRepliesCursorApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-replies-cursor-{Guid.NewGuid():N}.db");
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
    public async Task Replies_FinalPage_PersistsTheCallerSessionCursor()
    {
        var older = await SeedRepliedAsync(minutesAgo: 40);
        var newest = await SeedRepliedAsync(minutesAgo: 10);

        var replies = await RepliesAsync(withSession: true);

        replies.Items.Select(i => i.Id).ShouldBe([newest.Id, older.Id]);
        ReadReplyCursor().ShouldBe(newest.RepliedAt);
    }

    [Fact]
    public async Task Replies_WithoutTheSessionHeader_DoesNotAdvance()
    {
        await SeedRepliedAsync(minutesAgo: 40);
        await SeedRepliedAsync(minutesAgo: 10);

        var replies = await RepliesAsync(withSession: false);

        replies.Items.Length.ShouldBe(2);
        ReadReplyCursor().ShouldBeNull();
    }

    [Fact]
    public async Task Replies_UnknownSession_DoesNotAdvance()
    {
        await SeedRepliedAsync(minutesAgo: 10);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/agent/notifications/replies");
        request.Headers.Add(SessionHeader.Name, Guid.NewGuid().ToString());
        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReadReplyCursor().ShouldBeNull();
    }

    [Fact]
    public async Task Replies_FullPage_HoldsTheAdvanceUntilTheFinalPage()
    {
        var seeded = new List<DateTime>();
        for (var i = 1; i <= NotificationsService.ListCapacity; i++)
        {
            seeded.Add((await SeedRepliedAsync(minutesAgo: i)).RepliedAt);
        }

        var full = await RepliesAsync(withSession: true);

        full.Items.Length.ShouldBe(seeded.Count);
        ReadReplyCursor().ShouldBeNull();

        var newest = seeded.Max();
        var since = seeded[seeded.Count / 2];
        var expected = seeded.Count(t => t > since);
        var tail = await RepliesAsync(withSession: true, since);

        tail.Items.Length.ShouldBe(expected);
        tail.Items.ShouldNotContain(i => i.RepliedAt <= since);
        ReadReplyCursor().ShouldBe(newest);
    }

    [Fact]
    public async Task Replies_FirstConsumingRead_AdvancesFromNull_AndKeepsTheWakeDark()
    {
        var ancient = await SeedRepliedAsync(minutesAgo: 60 * 24 * 9);
        var recent = await SeedRepliedAsync(minutesAgo: 60 * 24);

        var birth = await WakeAsync();
        birth.Signals.ShouldBeEmpty();

        var replies = await RepliesAsync(withSession: true);

        replies.Items.Select(i => i.Id).ShouldBe([recent.Id, ancient.Id]);
        ReadReplyCursor().ShouldBe(recent.RepliedAt);

        var after = await WakeAsync();
        after.Signals.ShouldBeEmpty();
    }

    private DateTime? ReadReplyCursor()
    {
        using var command = _db.CreateCommand();
        command.CommandText =
            $"SELECT ReplyCursorAt FROM AgentSessions WHERE Id = '{AgentSessionId.ToString().ToUpperInvariant()}'";
        var raw = command.ExecuteScalar() as string;

        return raw is null
            ? null
            : DateTime.ParseExact(
                raw, "yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private async Task<SeededReply> SeedRepliedAsync(int minutesAgo)
    {
        var id = Guid.NewGuid();
        var repliedAt = Basis.AddMinutes(-minutesAgo);
        // schema-coupled on purpose: a replied notification with a chosen past RepliedAt and no
        // ReadAt stamp is only reachable through the table, never through the HTTP flow
        await ExecuteAsync($"""
            INSERT INTO AgentNotifications (Id, ProjectId, Kind, FeatureSlug, Tab, Text, Agent, CreatedAt, ReadAt, ReplyText, RepliedAt, HiddenAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', NULL, {(int)NotificationKind.Question}, NULL, NULL, 'ask {minutesAgo}', 'Dan', '{Timestamp(repliedAt)}', NULL, 'answer {minutesAgo}', '{Timestamp(repliedAt)}', NULL)
            """);

        return new SeededReply(id, repliedAt);
    }

    private async Task SeedAgentSessionAsync()
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, Role, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{AgentSessionId.ToString().ToUpperInvariant()}', 'Dan', 'claude', 'worker', '{Timestamp(DateTime.UtcNow)}', '{Timestamp(DateTime.UtcNow)}', NULL)
            """);
    }

    private async Task<RepliesJson> RepliesAsync(bool withSession, DateTime? since = null)
    {
        var url = "/api/agent/notifications/replies";
        if (since is { } from)
        {
            url += $"?since={Uri.EscapeDataString(from.ToString("O"))}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (withSession)
        {
            request.Headers.Add(SessionHeader.Name, AgentSessionId.ToString());
        }

        var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<RepliesJson>(JsonOptions)
            ?? throw new InvalidOperationException("replies body was null");
    }

    private async Task<WakeJson> WakeAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/agent/wake");
        request.Headers.Add(SessionHeader.Name, AgentSessionId.ToString());
        var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<WakeJson>(JsonOptions)
            ?? throw new InvalidOperationException("wake body was null");
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string Timestamp(DateTime at) => at.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF");

    private sealed record SeededReply(Guid Id, DateTime RepliedAt);

    private sealed record NotificationJson(Guid Id, DateTime? RepliedAt);

    private sealed record RepliesJson(NotificationJson[] Items);

    private sealed record WakeSignalJson(string Kind, Guid RefId, string? RoomId, string From, DateTime CreatedAt);

    private sealed record WakeJson(WakeSignalJson[] Signals, string Cursor);
}
