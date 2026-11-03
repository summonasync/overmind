using Microsoft.Extensions.AI;
using System.Text.Json;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class FakeAgentChatClient(params ChatResponseUpdate[][] rounds) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public ChatOptions? LastOptions { get; private set; }

    public bool AlwaysRequestTool { get; init; }

    public int RoundsServed { get; private set; }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add([.. chatMessages]);
        LastOptions = options;

        var round = AlwaysRequestTool
            ? [ToolCallUpdate($"c{RoundsServed + 1}", "planning_task_queue", $$"""{"projectId":"{{Guid.NewGuid()}}"}""")]
            : rounds[RoundsServed];

        RoundsServed++;
        foreach (var update in round)
        {
            await Task.Yield();
            yield return update;
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The loop only streams.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    void IDisposable.Dispose()
    {
    }

    public static ChatResponseUpdate TextUpdate(string text) => new(ChatRole.Assistant, text);

    public static ChatResponseUpdate ToolCallUpdate(string callId, string name, string jsonArguments) =>
        new()
        {
            Contents =
            [
                new FunctionCallContent(
                    callId,
                    name,
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonArguments))
            ]
        };

    public static ChatResponseUpdate UsageUpdate(long totalTokens) => new()
    {
        Contents = [new UsageContent(new UsageDetails { TotalTokenCount = totalTokens })]
    };

    public static ChatResponseUpdate UsageUpdate(long inputTokens, long outputTokens) => new()
    {
        Contents =
        [
            new UsageContent(new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens
            })
        ]
    };
}
