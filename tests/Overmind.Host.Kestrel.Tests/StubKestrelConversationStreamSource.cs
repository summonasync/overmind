using Overmind.Core;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Kestrel.Tests;

public sealed class StubKestrelConversationStreamSource(KestrelTurnStreamGate gate) : IConversationStreamSource
{
    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default,
        RoutingMissContext? missContext = null)
    {
        await Task.Yield();
        yield return ConversationStreamEvent.ToolCall("planning_task_queue");
        await gate.WaitAsync(ct);
        gate.ObserveCancellation(ct.IsCancellationRequested);
        yield return ConversationStreamEvent.Delta("stub delta one ");
        yield return ConversationStreamEvent.Delta("stub delta two");
        yield return ConversationStreamEvent.Completed(321);
    }
}
