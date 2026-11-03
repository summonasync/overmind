using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentChatApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    public AgentChatApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-agent-chat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Crud_CreateRenameListDelete_RoundTrips()
    {
        var project = await CreateProjectAsync();
        var created = await CreateConversationAsync(project.Id, "planning chat");

        created.ProjectId.ShouldBe(project.Id);
        created.Title.ShouldBe("planning chat");

        var renamed = await RenameAsync(created.Id, "renamed chat");
        renamed.Title.ShouldBe("renamed chat");

        var fetched = await GetAsync(created.Id);
        fetched.Title.ShouldBe("renamed chat");

        var list = await ListAsync(project.Id);
        list.Items.Single().Id.ShouldBe(created.Id);

        var unscopedList = await ListAsync(null);
        unscopedList.Items.Single().Id.ShouldBe(created.Id);

        var deleted = await _client.DeleteAsync($"/api/agent/conversations/{created.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/agent/conversations/{created.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_UnknownProject_ReturnsNotFoundProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/agent/conversations",
            new { ProjectId = Guid.NewGuid(), Title = (string?)"x" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stream_YieldsIncrementalFrames_AndPersistsBothMessages()
    {
        var conversation = await CreateConversationAsync(null, "stream chat");

        var frames = await StreamAsync(conversation.Id, "hello agent");

        var deltas = frames.Where(f => f.Event == "delta").ToList();
        deltas.Count.ShouldBeGreaterThanOrEqualTo(1);
        deltas[0].DataText.ShouldNotBeEmpty();

        var completed = frames.LastOrDefault(f => f.Event == "completed");
        completed.ShouldNotBeNull();
        var messageId = JsonDocument.Parse(completed.DataText).RootElement.GetProperty("messageId").GetString();
        messageId.ShouldNotBeNull();

        frames.ShouldNotContain(f => f.Event == "failed");

        var history = await MessagesAsync(conversation.Id);
        history.Items.Length.ShouldBe(2);
        history.Items[0].Role.ShouldBe("user");
        history.Items[0].Content.ShouldBe("hello agent");
        history.Items[1].Role.ShouldBe("assistant");
        history.Items[1].Content.ShouldNotBeEmpty();
        history.Items[1].Id.ShouldBe(Guid.Parse(messageId!));
    }

    [Fact]
    public async Task History_PagesBackward_WithLimitAndBefore()
    {
        var conversation = await CreateConversationAsync(null, "paged chat");
        await StreamAsync(conversation.Id, "one");
        await StreamAsync(conversation.Id, "two");

        var full = await MessagesAsync(conversation.Id);
        full.Items.Length.ShouldBe(4);
        full.Items.Select(i => i.Role).ShouldBe(["user", "assistant", "user", "assistant"]);

        var firstPage = await _client.GetAsync($"/api/agent/conversations/{conversation.Id}/messages?limit=2");
        firstPage.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paged = (await firstPage.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions))!;
        paged.Items.Length.ShouldBe(2);
        paged.Items[0].Content.ShouldBe("two");

        var before = full.Items[2].CreatedAt;
        var older = await _client.GetAsync(
            $"/api/agent/conversations/{conversation.Id}/messages?limit=2&before={Uri.EscapeDataString(before.ToString("O"))}");
        older.StatusCode.ShouldBe(HttpStatusCode.OK);
        var olderPage = (await older.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions))!;
        olderPage.Items.Select(i => i.Role).ShouldBe(["user", "assistant"]);
        olderPage.Items[0].Content.ShouldBe("one");
    }

    [Fact]
    public async Task UnknownConversation_Endpoints_Return404()
    {
        var missing = Guid.NewGuid();

        (await _client.GetAsync($"/api/agent/conversations/{missing}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/agent/conversations/{missing}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/agent/conversations/{missing}/messages")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await _client.PutAsJsonAsync($"/api/agent/conversations/{missing}", new { Title = "x" })).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        var stream = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{missing}/messages/stream", new { Text = "hi" }, JsonOptions);
        stream.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stream_EmptyText_ReturnsValidationProblem()
    {
        var conversation = await CreateConversationAsync(null, null);

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversation.Id}/messages/stream", new { Text = "" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stream_SecondPostWhileTurnInFlight_ReturnsConflict()
    {
        var conversation = await CreateConversationAsync(null, "parallel chat");
        var gate = _factory.Services.GetRequiredService<TurnStreamGate>();
        gate.Arm();

        var firstStream = await SendStreamRequestAsync(conversation.Id, "first");
        var second = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversation.Id}/messages/stream", new { Text = "second" }, JsonOptions);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        gate.Release();
        var frames = await SseTestClient.ReadAllFramesAsync(firstStream.Content);
        frames[0].Event.ShouldBe("delta");
        frames.Last().Event.ShouldBe("completed");
        var completed = System.Text.Json.JsonDocument.Parse(frames.Last().DataText);
        completed.RootElement.GetProperty("messageId").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task Stop_WhileTurnInFlight_AbortsTurnAndAllowsNewTurn()
    {
        var conversation = await CreateConversationAsync(null, "stop chat");
        var gate = _factory.Services.GetRequiredService<TurnStreamGate>();
        gate.Arm();

        var stream = await SendStreamRequestAsync(conversation.Id, "interrupt me");

        var stop = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversation.Id}/turn/stop", new { }, JsonOptions);
        stop.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await stop.Content.ReadFromJsonAsync<StopJson>(JsonOptions))!.Stopped.ShouldBeTrue();

        var frames = await SseTestClient.ReadAllFramesAsync(stream.Content);
        frames.ShouldNotBeEmpty();
        frames[0].Event.ShouldBe("delta");
        frames.Last().Event.ShouldBe("aborted");

        var history = await MessagesAsync(conversation.Id);
        history.Items.Select(i => i.Role).ShouldBe(["user"]);

        gate.Release();
        var restarted = await StreamAsync(conversation.Id, "try again");
        restarted.ShouldNotContain(f => f.Event == "failed");
        restarted.Last().Event.ShouldBe("completed");
    }

    private async Task<HttpResponseMessage> SendStreamRequestAsync(Guid conversationId, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/agent/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Text = text }, options: JsonOptions)
        };
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return response;
    }

    private async Task<List<SseFrame>> StreamAsync(Guid conversationId, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/agent/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Text = text }, options: JsonOptions)
        };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var body = await response.Content.ReadAsStringAsync();
        return ParseFrames(body);
    }

    private static List<SseFrame> ParseFrames(string body)
    {
        var frames = new List<SseFrame>();
        foreach (var chunk in body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? name = null;
            var data = new StringBuilder();
            foreach (var line in chunk.Split('\n'))
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    name = line["event: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data.Append(line["data: ".Length..]);
                }
            }

            if (name is not null)
            {
                frames.Add(new SseFrame(name, data.ToString()));
            }
        }

        return frames;
    }

    private async Task<ConversationJson> CreateConversationAsync(Guid? projectId, string? title)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/conversations",
            new { ProjectId = projectId, Title = title }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions))!;
    }

    private async Task<ConversationJson> RenameAsync(Guid id, string title)
    {
        var response = await _client.PutAsJsonAsync($"/api/agent/conversations/{id}", new { Title = title }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions))!;
    }

    private async Task<ConversationJson> GetAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/agent/conversations/{id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions))!;
    }

    private async Task<ConversationsJson> ListAsync(Guid? projectId)
    {
        var url = projectId is { } scoped
            ? $"/api/agent/conversations?projectId={scoped}"
            : "/api/agent/conversations";
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ConversationsJson>(JsonOptions))!;
    }

    private async Task<HistoryJson> MessagesAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/agent/conversations/{id}/messages");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions))!;
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"agent-chat-{Guid.NewGuid():N}",
            DiskPath = _sandboxDir,
            Description = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private sealed record ConversationJson(Guid Id, Guid? ProjectId, string? Title);

    private sealed record ConversationsJson(ConversationJson[] Items);

    private sealed record MessageJson(Guid Id, string Role, string Content, DateTime CreatedAt);

    private sealed record HistoryJson(MessageJson[] Items);

    private sealed record StopJson(bool Stopped);

    private sealed record ProjectJson(Guid Id);
}
