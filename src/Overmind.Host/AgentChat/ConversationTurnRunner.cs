using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.AgentChat;

public sealed class ConversationTurnRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<ConversationTurnRunner> logger)
{
    public async Task RunAsync(
        ConversationTurn turn,
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        Func<Guid, ConversationTurn, bool> removeFromRegistry)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var source = scope.ServiceProvider.GetRequiredService<IConversationStreamSource>();
            var messages = scope.ServiceProvider.GetRequiredService<ConversationMessageStore>();
            var ended = await StreamTurnAsync(turn, conversation, history, userText, source, messages);
            if (!ended)
            {
                turn.Fail("The chat stream ended without a terminal event.", null);
            }
        }
        catch (OperationCanceledException) when (turn.CancellationToken.IsCancellationRequested)
        {
            turn.Abort();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Agent turn for conversation {ConversationId} was cancelled externally",
                conversation.Id);
            turn.Fail("The chat stream failed.", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent turn for conversation {ConversationId} failed", conversation.Id);
            turn.Fail("The chat stream failed.", null);
        }
        finally
        {
            removeFromRegistry(turn.ConversationId, turn);
            turn.DisposeLifetime();
        }
    }

    private async Task<bool> StreamTurnAsync(
        ConversationTurn turn,
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        IConversationStreamSource source,
        ConversationMessageStore messages)
    {
        await foreach (var streamEvent in source.StreamAsync(conversation, history, userText, turn.CancellationToken))
        {
            switch (streamEvent.Kind)
            {
                case ConversationStreamEventKind.Delta:
                    turn.AppendReply(streamEvent.Text!);
                    turn.Publish(streamEvent);
                    break;
                case ConversationStreamEventKind.ToolCall:
                case ConversationStreamEventKind.Routed:
                    turn.Publish(streamEvent);
                    break;
                case ConversationStreamEventKind.Bounced:
                    break;
                case ConversationStreamEventKind.Completed:
                    Guid? messageId = turn.HasReply
                        ? (await messages.AppendAsync(
                            conversation.Id, ConversationMessage.AssistantRole, turn.ReplyText, CancellationToken.None)).Id
                        : null;
                    turn.Complete(streamEvent.TokensUsed, messageId);
                    return true;
                case ConversationStreamEventKind.Failed:
                    turn.Fail(streamEvent.Text ?? "The chat stream failed.", streamEvent.Code);
                    return true;
                default:
                    turn.Fail("The chat stream sent an unknown event.", "unknownStreamEvent");
                    return true;
            }
        }

        return false;
    }
}
