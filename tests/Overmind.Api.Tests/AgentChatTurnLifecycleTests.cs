using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Host.AgentChat;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentChatTurnLifecycleTests : IClassFixture<AgentChatHostFixture>, IAsyncLifetime
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly AgentChatHostFixture _host;
    private readonly HttpClient _client;

    public AgentChatTurnLifecycleTests(AgentChatHostFixture host)
    {
        _host = host;
        _client = host.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _host.Gate.Release();
        _client.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DetachedSubscriber_CancelledMidTurn_TurnCompletesAndPersistsReply()
    {
        var conversationId = await CreateConversationAsync("reload repro");
        _host.Gate.Arm();

        var (turn, subscriber) = await StartTurnAndSubscribeAsync(conversationId);
        (await subscriber.MoveNextAsync()).ShouldBeTrue();

        await subscriber.DisposeAsync();
        _host.Gate.Release();

        var (reloadedClient, scope) = OpenReloadedScope();
        try
        {
            var messages = scope.ServiceProvider.GetRequiredService<ConversationMessageStore>();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var history = await messages.ListPageAsync(conversationId, ct: CancellationToken.None);
                if (history.LastOrDefault(m => m.Role == ConversationMessage.AssistantRole) is { } reply)
                {
                    reply.Content.ShouldBe("stub delta one stub delta two");
                    return;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException("The assistant reply was not persisted after the subscriber detached.");
        }
        finally
        {
            scope.Dispose();
            reloadedClient.Dispose();
        }
    }

    [Fact]
    public async Task Attach_AfterSubscriberGone_ReplaysSnapshotAndResumesLiveFrames()
    {
        var conversationId = await CreateConversationAsync("reattach");
        _host.Gate.Arm();

        var (turn, subscriber) = await StartTurnAndSubscribeAsync(conversationId);
        (await subscriber.MoveNextAsync()).ShouldBeTrue();
        await subscriber.DisposeAsync();

        using var reattached = await _client.GetAsync(
            $"/api/agent/conversations/{conversationId}/turn/stream", HttpCompletionOption.ResponseHeadersRead);
        reattached.StatusCode.ShouldBe(HttpStatusCode.OK);

        _host.Gate.Release();

        var frames = await SseTestClient.ReadAllFramesAsync(reattached.Content);
        frames.ShouldNotBeEmpty();
        frames[0].Event.ShouldBe("delta");
        frames[0].DataText.ShouldBe("""{"text":"stub delta one "}""");
        frames.Last().Event.ShouldBe("completed");
        var messageId = System.Text.Json.JsonDocument.Parse(frames.Last().DataText)
            .RootElement.GetProperty("messageId").GetString();
        messageId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Attach_NoTurnInFlight_ReturnsNoContent()
    {
        var conversationId = await CreateConversationAsync("idle attach");

        using var response = await _client.GetAsync(
            $"/api/agent/conversations/{conversationId}/turn/stream", HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateConversationAsync(string title)
    {
        using var scope = _host.Services.CreateAsyncScope();
        var conversations = scope.ServiceProvider.GetRequiredService<ConversationStore>();
        var conversation = await conversations.CreateAsync(null, title, CancellationToken.None);
        return conversation.Id;
    }

    private async Task<(ConversationTurn Turn, IAsyncEnumerator<ConversationStreamEvent> Subscriber)>
        StartTurnAndSubscribeAsync(Guid conversationId)
    {
        using var scope = _host.Services.CreateAsyncScope();
        var conversations = scope.ServiceProvider.GetRequiredService<ConversationStore>();
        var messages = scope.ServiceProvider.GetRequiredService<ConversationMessageStore>();
        var conversation = await conversations.GetAsync(conversationId, CancellationToken.None);
        var history = await messages.ListPageAsync(conversationId, ct: CancellationToken.None);

        return StartTurnAndSubscribe(conversation, history);
    }

    private (ConversationTurn Turn, IAsyncEnumerator<ConversationStreamEvent> Subscriber)
        StartTurnAndSubscribe(Conversation conversation, IReadOnlyList<ConversationMessage> history)
    {
        var registry = _host.Services.GetRequiredService<ConversationTurnRegistry>();
        var turn = registry.TryCreate(conversation) ??
            throw new InvalidOperationException("The turn was refused to start.");
        var subscriber = turn.Subscribe().GetAsyncEnumerator();
        registry.Start(turn, conversation, history, "hello");
        return (turn, subscriber);
    }

    private (HttpClient Client, IServiceScope Scope) OpenReloadedScope()
    {
        var client = _host.CreateClient();
        var scope = _host.Services.CreateAsyncScope();
        return (client, scope);
    }
}
