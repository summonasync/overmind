using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Llm;

namespace Overmind.Infrastructure.Agent;

public sealed class RouterChatResponder(
    IRouterPromptComposer prompts,
    ILlmChatClientFactory chatFactory,
    IAgentBudgetStore budget,
    ILogger<RouterChatResponder> logger)
{
    public const string ProviderMissingNotice = LlmConversationStreamSource.ProviderMissingNotice;

    public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        IReadOnlyList<AgentRouteRecord> hotHead,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = await chatFactory.CreateAsync(ct);
        if (client is null)
        {
            yield return ConversationStreamEvent.Delta(ProviderMissingNotice);
            yield return ConversationStreamEvent.Completed();
            yield break;
        }

        var messages = await prompts.ComposeChatAsync(conversation, history, hotHead, userText, ct);
        var text = new StringBuilder();
        long outputTokens = 0;
        await foreach (var update in client.GetStreamingResponseAsync(messages, options: null, ct))
        {
            if (update.Text.Length > 0)
            {
                text.Append(update.Text);
                yield return ConversationStreamEvent.Delta(update.Text);
            }

            foreach (var content in update.Contents)
            {
                if (content is UsageContent usage)
                {
                    var updateOutput = usage.Details.OutputTokenCount
                        ?? usage.Details.TotalTokenCount
                        ?? usage.Details.InputTokenCount
                        ?? 0;
                    outputTokens += updateOutput;
                }
            }
        }

        await SpendAsync(outputTokens, ct);
        logger.LogInformation(
            "Router answered conversation {ConversationId} directly using {OutputTokens} output tokens",
            conversation.Id, outputTokens);
        yield return ConversationStreamEvent.Completed();
    }

    private async Task SpendAsync(long outputTokens, CancellationToken ct)
    {
        if (outputTokens <= 0)
        {
            return;
        }

        try
        {
            await budget.AddUsageAsync(AgentBudgetDay.Current(), outputTokens, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Router chat usage was not recorded for today");
        }
    }
}
