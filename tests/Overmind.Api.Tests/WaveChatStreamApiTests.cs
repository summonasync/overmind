using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Host.Api;
using Overmind.Host.WaveChat;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Wave;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class WaveChatStreamApiTests : IDisposable
{
    private static readonly Guid AgentSessionId = Guid.NewGuid();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;
    private readonly List<HttpResponseMessage> _streams = [];

    public WaveChatStreamApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-wave-stream-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
        SeedAgentSessionAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        foreach (var stream in _streams)
        {
            stream.Dispose();
        }

        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Stream_HumanDoorPost_PushesFrameToOpenSubscriber()
    {
        var projectId = await CreateProjectAsync();
        var reader = await OpenStreamAsync(projectId);

        await PostHumanAsync(projectId, "all", "Dan", "push me");

        var frame = await ReadMessageFrameAsync(reader);
        frame.Room.ShouldBe("all");
        frame.Message["text"]!.GetValue<string>().ShouldBe("push me");
        frame.Message["authorAlias"]!.GetValue<string>().ShouldBe("Dan");
        frame.Message["authorRole"]!.GetValue<string>().ShouldBe("owner");
    }

    [Fact]
    public async Task Stream_AgentDoorPost_PushesFrameToOpenSubscriber()
    {
        var projectId = await CreateProjectAsync();
        var reader = await OpenStreamAsync(projectId);

        var response = await SendAgentAsync(HttpMethod.Post,
            $"/api/agent/projects/{projectId}/wave/rooms/all/messages", new { Text = "from the adapter" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var frame = await ReadMessageFrameAsync(reader);
        frame.Room.ShouldBe("all");
        frame.Message["text"]!.GetValue<string>().ShouldBe("from the adapter");
        frame.Message["authorAlias"]!.GetValue<string>().ShouldBe("Dan");
        frame.Message["authorRole"]!.GetValue<string>().ShouldBe("worker");
    }

    [Fact]
    public async Task Stream_LateSubscriberWithSince_GetsBackfillThenLiveWithoutGapOrDuplication()
    {
        var projectId = await CreateProjectAsync();
        var first = await PostMessageAsync(projectId, "one");
        await PostMessageAsync(projectId, "two");

        var reader = await OpenStreamAsync(projectId, since: first);

        await PostHumanAsync(projectId, "all", "Dan", "three");

        var boundary = await ReadMessageFrameAsync(reader);
        boundary.Message["text"]!.GetValue<string>().ShouldBe("two");
        var live = await ReadMessageFrameAsync(reader);
        live.Message["text"]!.GetValue<string>().ShouldBe("three");

        await AssertStreamQuietAsync(reader);
    }

    [Fact]
    public async Task Stream_NoSince_ConnectsLiveOnlyWithoutBackfill()
    {
        var projectId = await CreateProjectAsync();
        await PostHumanAsync(projectId, "all", "Dan", "older one");
        await PostHumanAsync(projectId, "all", "Dan", "older two");

        var reader = await OpenStreamAsync(projectId);
        await PostHumanAsync(projectId, "all", "Dan", "live only");

        var frame = await ReadMessageFrameAsync(reader);
        frame.Message["text"]!.GetValue<string>().ShouldBe("live only");

        await AssertStreamQuietAsync(reader);
    }

    [Fact]
    public async Task Stream_CompositeCursor_ResumesPastTiedTimestampsWithoutSkipping()
    {
        var projectId = await CreateProjectAsync();
        await PostHumanAsync(projectId, "all", "Dan", "one");
        var tiedAt = await PostMessageAsync(projectId, "two");
        await TieCreatedAtAsync(projectId, tiedAt);

        using var scope = _factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<WaveChatService>();
        var ordered = await chat.ListSinceAsync(projectId, tiedAt.AddMilliseconds(-1));
        var cursor = ordered[0];

        var reader = await OpenStreamAsync(projectId, since: tiedAt, sinceId: cursor.Id);
        await PostHumanAsync(projectId, "all", "Dan", "three");

        var boundary = await ReadMessageFrameAsync(reader);
        boundary.Message["text"]!.GetValue<string>().ShouldBe(ordered[1].Text);
        var live = await ReadMessageFrameAsync(reader);
        live.Message["text"]!.GetValue<string>().ShouldBe("three");

        await AssertStreamQuietAsync(reader);
    }

    [Fact]
    public async Task Stream_FrameMessage_CarriesTheHistoryEndpointDtoShape()
    {
        var projectId = await CreateProjectAsync();
        var reader = await OpenStreamAsync(projectId);

        await PostHumanAsync(projectId, "all", "Dan", "same shape @dan");

        var frame = await ReadMessageFrameAsync(reader);
        var history = await _client.GetFromJsonAsync<HistoryJson>(
            $"/api/projects/{projectId}/wave/rooms/all/messages", JsonOptions);
        var stored = history!.Items.Single();

        frame.Message["id"]!.GetValue<string>().ShouldBe(stored.Id.ToString());
        frame.Message["room"]!.GetValue<string>().ShouldBe(stored.Room);
        frame.Message["authorAlias"]!.GetValue<string>().ShouldBe(stored.AuthorAlias);
        frame.Message["authorRole"]!.GetValue<string>().ShouldBe(stored.AuthorRole);
        frame.Message["text"]!.GetValue<string>().ShouldBe(stored.Text);
        frame.Message["mentions"]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldBe(stored.Mentions);
        frame.Message["createdAt"]!.GetValue<string>().ShouldNotBeNull();
    }

    [Fact]
    public async Task Stream_UnknownProject_Returns404WithNamedCode()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/wave/stream");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        problem["title"]!.GetValue<string>().ShouldBe("MissingProject");
    }

    [Fact]
    public async Task Stream_EmitsHeartbeatCommentFrames()
    {
        var projectId = await CreateProjectAsync();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<WaveStreamOptions>();
            services.AddSingleton(new WaveStreamOptions(TimeSpan.FromMilliseconds(200)));
        }));
        using var client = factory.CreateClient();
        var reader = await OpenStreamAsync(client, projectId);

        var raw = await reader.ReadFrameTextAsync(TimeoutToken(5000));

        raw.ShouldBe(": ping\n\n");
    }

    [Fact]
    public async Task Stream_ClientDisconnect_RemovesHubSubscriber()
    {
        var projectId = await CreateProjectAsync();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<WaveStreamOptions>();
            services.AddSingleton(new WaveStreamOptions(TimeSpan.FromMilliseconds(200)));
        }));
        using var client = factory.CreateClient();
        var hub = factory.Services.GetRequiredService<WaveSseHub>();
        await OpenStreamAsync(client, projectId);

        hub.CountSubscribers(projectId).ShouldBe(1);

        _streams[^1].Dispose();
        for (var attempt = 0; attempt < 50 && hub.CountSubscribers(projectId) > 0; attempt++)
        {
            await Task.Delay(100);
        }

        hub.CountSubscribers(projectId).ShouldBe(0);
    }

    private async Task<SseFrameReader> OpenStreamAsync(
        HttpClient client, Guid projectId, DateTime? since = null, Guid? sinceId = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (since is { } anchor)
        {
            query.Add($"since={Uri.EscapeDataString(anchor.ToString("O"))}");
        }

        if (sinceId is { } cursor)
        {
            query.Add($"sinceId={cursor}");
        }

        var target = query.Count > 0
            ? $"/api/projects/{projectId}/wave/stream?{string.Join("&", query)}"
            : $"/api/projects/{projectId}/wave/stream";
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, target),
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        _streams.Add(response);
        return new SseFrameReader(await response.Content.ReadAsStreamAsync(ct));
    }

    private async Task<SseFrameReader> OpenStreamAsync(Guid projectId, DateTime? since = null, Guid? sinceId = null) =>
        await OpenStreamAsync(_client, projectId, since, sinceId);

    private async Task<WaveStreamFrameJson> ReadMessageFrameAsync(SseFrameReader reader)
    {
        while (true)
        {
            var raw = await reader.ReadFrameTextAsync(TimeoutToken(10_000));
            raw.ShouldNotBeNull();
            var frame = SseTestClient.ParseFrame(raw);
            if (frame is null || frame.Event != "message")
            {
                continue;
            }

            var payload = JsonNode.Parse(frame.DataText)!;
            return new WaveStreamFrameJson(
                payload["room"]!.GetValue<string>(),
                payload["message"]!.AsObject());
        }
    }

    private static async Task AssertStreamQuietAsync(SseFrameReader reader)
    {
        try
        {
            var frame = await reader.ReadFrameTextAsync(TimeoutToken(400));
            frame.ShouldBeNull();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static CancellationToken TimeoutToken(int milliseconds) =>
        new CancellationTokenSource(milliseconds).Token;

    private async Task<HttpResponseMessage> PostHumanAsync(
        Guid projectId, string room, string authorAlias, string text) =>
        await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/wave/rooms/{room}/messages",
            new { AuthorAlias = authorAlias, Text = text }, JsonOptions);

    private async Task TieCreatedAtAsync(Guid projectId, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        await db.WaveMessages
            .Where(m => m.ProjectId == projectId)
            .ExecuteUpdateAsync(setter => setter.SetProperty(m => m.CreatedAt, createdAt));
    }

    private async Task<DateTime> PostMessageAsync(Guid projectId, string text)
    {
        var response = await PostHumanAsync(projectId, "all", "Dan", text);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var message = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return message.GetProperty("createdAt").GetDateTime();
    }

    private async Task<HttpResponseMessage> SendAgentAsync(
        HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(SessionHeader.Name, AgentSessionId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var name = $"wave-stream-{Guid.NewGuid():N}";
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

    private async Task SeedAgentSessionAsync()
    {
        using var command = _db.CreateCommand();
        command.CommandText = $"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{AgentSessionId.ToString().ToUpperInvariant()}', 'Dan', 'claude', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record HistoryJson(MessageJson[] Items);

    private sealed record MessageJson(
        Guid Id, string Room, string AuthorAlias, string Text, string[] Mentions, DateTime CreatedAt,
        string AuthorRole);

    private sealed record WaveStreamFrameJson(string Room, JsonObject Message);
}
