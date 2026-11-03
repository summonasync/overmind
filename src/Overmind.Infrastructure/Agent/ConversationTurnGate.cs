using System.Collections.Concurrent;

namespace Overmind.Infrastructure.Agent;

public sealed class ConversationTurnGate
{
    private readonly ConcurrentDictionary<Guid, int> _inFlight = new();

    public bool TryEnter(Guid conversationId)
    {
        var running = _inFlight.AddOrUpdate(conversationId, 1, (_, count) => count + 1) > 1;
        if (running)
        {
            Exit(conversationId);
        }

        return !running;
    }

    public void Enter(Guid conversationId)
    {
        _inFlight.AddOrUpdate(conversationId, 1, (_, count) => count + 1);
    }

    public void Exit(Guid conversationId)
    {
        var left = _inFlight.AddOrUpdate(conversationId, 0, (_, count) => count - 1);
        if (left <= 0)
        {
            _inFlight.TryRemove(KeyValuePair.Create(conversationId, 0));
        }
    }

    public bool IsBusy(Guid conversationId) =>
        _inFlight.TryGetValue(conversationId, out var count) && count > 0;
}
