using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Host.AgentChat;

public sealed class ConversationTurnRegistry(
    ConversationTurnRunner runner,
    ILogger<ConversationTurnRegistry> logger)
{
    private readonly ConcurrentDictionary<Guid, ConversationTurn> _turns = new();

    public ConversationTurn? TryCreate(Conversation conversation)
    {
        var turn = new ConversationTurn(conversation.Id);
        return _turns.TryAdd(conversation.Id, turn) ? turn : null;
    }

    public void Start(
        ConversationTurn turn,
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText)
    {
        _ = RunQuietlyAsync(turn, conversation, history, userText);
    }

    public ConversationTurn? Find(Guid conversationId) =>
        _turns.TryGetValue(conversationId, out var turn) ? turn : null;

    public bool TryStop(Guid conversationId)
    {
        if (_turns.TryGetValue(conversationId, out var turn))
        {
            turn.RequestStop();
            return true;
        }

        return false;
    }

    private async Task RunQuietlyAsync(
        ConversationTurn turn,
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText)
    {
        try
        {
            await runner.RunAsync(turn, conversation, history, userText, Remove);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent turn for conversation {ConversationId} ended unexpectedly", conversation.Id);
        }
    }

    private bool Remove(Guid conversationId, ConversationTurn turn) =>
        _turns.TryRemove(KeyValuePair.Create(conversationId, turn));
}
