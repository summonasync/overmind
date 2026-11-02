using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentChatReadApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentChatReadApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Unread_GrowsWithAssistantMessages_AndDropsOnMarkRead()
    {
        var conversationId = await CreateConversationAsync();
        (await UnreadCountAsync()).ShouldBe(0);

        await StreamReplyAsync(conversationId);
        (await UnreadCountAsync()).ShouldBe(1);

        await StreamReplyAsync(conversationId);
        (await UnreadCountAsync()).ShouldBe(2);

        var assistantIds = await AssistantMessageIdsAsync(conversationId);
        var state = await MarkReadAsync(conversationId, assistantIds[^1]);

        state.ConversationId.ShouldBe(conversationId);
        state.LastReadMessageId.ShouldBe(assistantIds[^1]);
        (await UnreadCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task MarkRead_OlderMessage_IsNoOpAndKeepsWatermark()
    {
        var conversationId = await CreateConversationAsync();
        await StreamReplyAsync(conversationId);
        await StreamReplyAsync(conversationId);
        var assistantIds = await AssistantMessageIdsAsync(conversationId);

        await MarkReadAsync(conversationId, assistantIds[^1]);
        (await UnreadCountAsync()).ShouldBe(0);

        var stale = await MarkReadAsync(conversationId, assistantIds[0]);

        stale.LastReadMessageId.ShouldBe(assistantIds[^1]);
        (await UnreadCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Unread_WatermarkBoundary_CountsOnlyMessagesAfterIt()
    {
        var conversationId = await CreateConversationAsync();
        await StreamReplyAsync(conversationId);
        await StreamReplyAsync(conversationId);
        var assistantIds = await AssistantMessageIdsAsync(conversationId);

        await MarkReadAsync(conversationId, assistantIds[0]);

        (await UnreadCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task MarkRead_UnknownConversation_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{Guid.NewGuid()}/read",
            new { LastReadMessageId = Guid.NewGuid() }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("MissingConversation");
    }

    [Fact]
    public async Task MarkRead_UnknownOrForeignMessage_ReturnsNotFound()
    {
        var conversationId = await CreateConversationAsync();
        await StreamReplyAsync(conversationId);
        var otherConversationId = await CreateConversationAsync();
        await StreamReplyAsync(otherConversationId);

        var unknown = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversationId}/read",
            new { LastReadMessageId = Guid.NewGuid() }, JsonOptions);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await unknown.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("MissingConversationMessage");

        var foreign = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversationId}/read",
            new { LastReadMessageId = (await AssistantMessageIdsAsync(otherConversationId))[0] }, JsonOptions);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unread_ToolAndUserRows_NeverCount()
    {
        var conversationId = await CreateConversationAsync();
        using (var scope = _factory.Services.CreateAsyncScope())
        {
            var messages = scope.ServiceProvider.GetRequiredService<ConversationMessageStore>();
            await messages.AppendAsync(conversationId, ConversationMessage.UserRole, "seeded user line");
            await messages.AppendAsync(conversationId, ConversationMessage.ToolRole, "seeded tool row");
            await messages.AppendAsync(conversationId, ConversationMessage.SystemRole, "seeded system row");
        }

        (await UnreadCountAsync()).ShouldBe(0);

        await StreamReplyAsync(conversationId);
        (await UnreadCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Unread_AggregatesProjectScoped_AndUnscopedConversations()
    {
        var project = await CreateProjectAsync();
        var unscopedId = await CreateConversationAsync();
        var scopedId = await CreateConversationAsync(project.Id);

        await StreamReplyAsync(unscopedId);
        await StreamReplyAsync(unscopedId);
        await StreamReplyAsync(scopedId);

        (await UnreadCountAsync()).ShouldBe(3);

        var unscopedIds = await AssistantMessageIdsAsync(unscopedId);
        await MarkReadAsync(unscopedId, unscopedIds[^1]);
        (await UnreadCountAsync()).ShouldBe(1);

        var scopedIds = await AssistantMessageIdsAsync(scopedId);
        await MarkReadAsync(scopedId, scopedIds[^1]);
        (await UnreadCountAsync()).ShouldBe(0);
    }

    private async Task<Guid> CreateConversationAsync(Guid? projectId = null)
    {
        var created = await _client.PostAsJsonAsync(
            "/api/agent/conversations", new { ProjectId = projectId, Title = "read tracking" }, JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions))!.Id;
    }

    private async Task StreamReplyAsync(Guid conversationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/agent/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Text = "ping" }, options: JsonOptions)
        };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("event: failed");
    }

    private async Task<List<Guid>> AssistantMessageIdsAsync(Guid conversationId)
    {
        var history = await _client.GetFromJsonAsync<HistoryJson>(
            $"/api/agent/conversations/{conversationId}/messages?limit=100", JsonOptions);
        return history!.Items
            .Where(m => m.Role == ConversationMessage.AssistantRole)
            .Select(m => m.Id)
            .ToList();
    }

    private async Task<int> UnreadCountAsync()
    {
        var response = await _client.GetAsync("/api/agent/conversations/unread");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UnreadJson>(JsonOptions))!.Count;
    }

    private async Task<ReadStateJson> MarkReadAsync(Guid conversationId, Guid lastReadMessageId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversationId}/read",
            new { LastReadMessageId = lastReadMessageId }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ReadStateJson>(JsonOptions))!;
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"read-tracking-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = Path.Combine(Path.GetTempPath(), name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private sealed record ConversationJson(Guid Id, Guid? ProjectId, string? Title);

    private sealed record HistoryJson(MessageJson[] Items);

    private sealed record MessageJson(Guid Id, string Role, string Content, DateTime CreatedAt);

    private sealed record ReadStateJson(Guid ConversationId, Guid? LastReadMessageId);

    private sealed record UnreadJson(int Count);

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
