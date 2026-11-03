using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Infrastructure.Agent;

public sealed class RouterConversationStreamSource(
    IntentRouter router,
    RouterChatResponder chat,
    [FromKeyedServices(RouterConversationStreamSource.WorkerStreamSourceKey)] IConversationStreamSource worker,
    ILogger<RouterConversationStreamSource> logger) : IConversationStreamSource
{
    public const string WorkerStreamSourceKey = "worker";

    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [EnumeratorCancellation] CancellationToken ct = default,
        RoutingMissContext? missContext = null)
    {
        var hotHead = await router.GetHotHeadAsync(ct);
        var decision = await router.RouteAsync(conversation, history, userText, hotHead, ct);
        var bounces = 0;
        var tokensUsed = 0L;

        while (true)
        {
            yield return ConversationStreamEvent.Routed(decision.Target, decision.Source);
            logger.LogInformation(
                "Router routed conversation {ConversationId} to {Target} via {Source} after {Bounces} bounces",
                conversation.Id, decision.Target, decision.Source, bounces);

            if (decision.Target == RoutingTargets.Chat)
            {
                await foreach (var streamEvent in chat.StreamAsync(conversation, history, userText, hotHead, ct))
                {
                    if (streamEvent.Kind == ConversationStreamEventKind.Completed)
                    {
                        tokensUsed += streamEvent.TokensUsed ?? 0;
                        yield return ConversationStreamEvent.Completed(ToNullable(tokensUsed));
                        yield break;
                    }

                    yield return streamEvent;
                }

                yield break;
            }

            string? bounceReason = null;
            var completed = false;
            await foreach (var streamEvent in worker.StreamAsync(conversation, history, userText, ct, decision.Miss))
            {
                switch (streamEvent.Kind)
                {
                    case ConversationStreamEventKind.Bounced:
                        bounceReason = streamEvent.Text ?? "no reason given";
                        break;
                    case ConversationStreamEventKind.Completed:
                        tokensUsed += streamEvent.TokensUsed ?? 0;
                        completed = true;
                        break;
                    case ConversationStreamEventKind.Failed:
                    case ConversationStreamEventKind.Aborted:
                        yield return streamEvent;
                        yield break;
                    default:
                        yield return streamEvent;
                        break;
                }

                if (completed)
                {
                    break;
                }
            }

            if (!completed || bounceReason is null)
            {
                yield return ConversationStreamEvent.Completed(ToNullable(tokensUsed));
                yield break;
            }

            bounces++;
            decision = await router.ReRouteAsync(
                bounceReason, decision.Target, bounces, conversation, history, userText, hotHead, ct);
        }
    }

    private static long? ToNullable(long tokens) => tokens > 0 ? tokens : null;
}
