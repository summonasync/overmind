using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Llm;

namespace Overmind.Infrastructure.Agent;

public sealed class IntentRouter(
    IRouterPromptComposer prompts,
    ILlmChatClientFactory chatFactory,
    IAgentRouteStore routes,
    IRouteVectorIndex routeVectors,
    IAgentBudgetStore budget,
    ILogger<IntentRouter> logger)
{
    public const int HotHeadSize = 12;
    public const int MaxBounces = 2;
    public const string ClassificationFailedReason = "classification failed";
    public const string ClassificationUnparseableReason = "classification output was unparseable";
    public const string NoProviderReason = "no provider configured";

    public async Task<IReadOnlyList<AgentRouteRecord>> GetHotHeadAsync(CancellationToken ct) =>
        await routes.HotHeadAsync(HotHeadSize, ct);

    public async Task<RoutingDecision> RouteAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        IReadOnlyList<AgentRouteRecord> hotHead,
        CancellationToken ct)
    {
        if (TryStructuralRoute(conversation, history, userText) is { } structural)
        {
            return structural;
        }

        var match = hotHead.FirstOrDefault(route => AgentRouteText.Matches(userText, route.Pattern));
        if (match is not null)
        {
            logger.LogInformation(
                "Router matched conversation {ConversationId} to learned route {Pattern} targeting {Target}",
                conversation.Id, match.Pattern, match.TargetAgent ?? RoutingTargets.Worker);
            return new RoutingDecision(
                match.TargetAgent ?? RoutingTargets.Worker,
                RoutingSource.Learned,
                new RoutingMissContext($"learned pattern match: {match.Pattern}", hotHead.Count));
        }

        if (await routeVectors.MatchTopAsync(userText, ct) is { } semantic && semantic.IsRoutable)
        {
            logger.LogInformation(
                "Router matched conversation {ConversationId} to semantically similar route {RouteId} in {Domain} with score {Score}",
                conversation.Id, semantic.RouteId, semantic.Domain, semantic.Score);
            return new RoutingDecision(
                semantic.Target ?? RoutingTargets.Worker,
                RoutingSource.Learned,
                new RoutingMissContext(
                    $"semantic route match: {semantic.Domain} (score {semantic.Score:F2})", hotHead.Count));
        }

        return await ClassifyAsync(conversation, history, userText, hotHead, ct);
    }

    public async Task<RoutingDecision> ReRouteAsync(
        string bounceReason,
        string bouncedTarget,
        int bouncesSoFar,
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        IReadOnlyList<AgentRouteRecord> hotHead,
        CancellationToken ct)
    {
        if (bouncesSoFar >= MaxBounces)
        {
            return Fallback($"bounce budget exhausted after {bouncesSoFar} bounces: {bounceReason}", hotHead.Count);
        }

        var decision = await RouteAsync(conversation, history, userText, hotHead, ct);
        if (decision.Target == bouncedTarget)
        {
            return Fallback($"re-route landed on the bounced target {bouncedTarget}: {bounceReason}", hotHead.Count);
        }

        return decision;
    }

    private static RoutingDecision? TryStructuralRoute(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText) =>
        null;

    private async Task<RoutingDecision> ClassifyAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        IReadOnlyList<AgentRouteRecord> hotHead,
        CancellationToken ct)
    {
        var client = await chatFactory.CreateAsync(ct);
        if (client is null)
        {
            return Fallback(NoProviderReason, hotHead.Count);
        }

        long outputTokens;
        string output;
        try
        {
            var messages = await prompts.ComposeClassificationAsync(conversation, history, hotHead, userText, ct);
            var text = new StringBuilder();
            outputTokens = await CollectAsync(client, messages, text, ct);
            output = text.ToString();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Router classification call failed for conversation {ConversationId}", conversation.Id);
            return Fallback(ClassificationFailedReason, hotHead.Count);
        }

        var parsed = ParseClassification(output);
        if (parsed is null)
        {
            return Fallback(ClassificationUnparseableReason, hotHead.Count);
        }

        await SpendAsync(outputTokens, ct);
        logger.LogInformation(
            "Router classified conversation {ConversationId} to {Target} using {OutputTokens} output tokens",
            conversation.Id, parsed.Target, outputTokens);

        return parsed.Target == RoutingTargets.Chat
            ? new RoutingDecision(
                RoutingTargets.Chat,
                RoutingSource.Classification,
                new RoutingMissContext($"classifier chose chat: {parsed.Reason}", hotHead.Count))
            : Fallback($"classifier routed to worker: {parsed.Reason}", hotHead.Count);
    }

    private static async Task<long> CollectAsync(
        IChatClient client,
        IReadOnlyList<ChatMessage> messages,
        StringBuilder text,
        CancellationToken ct)
    {
        long outputTokens = 0;
        await foreach (var update in client.GetStreamingResponseAsync(messages, options: null, ct))
        {
            if (update.Text.Length > 0)
            {
                text.Append(update.Text);
            }

            foreach (var content in update.Contents)
            {
                if (content is UsageContent usage)
                {
                    outputTokens += usage.Details.OutputTokenCount
                        ?? usage.Details.TotalTokenCount
                        ?? usage.Details.InputTokenCount
                        ?? 0;
                }
            }
        }

        return outputTokens;
    }

    private static ParsedClassification? ParseClassification(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(output[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("target", out var target))
            {
                return null;
            }

            var targetValue = target.GetString()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(targetValue))
            {
                return null;
            }

            var reason = root.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString()?.Trim()
                : null;
            return new ParsedClassification(targetValue, reason ?? "no reason given");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private RoutingDecision Fallback(string reason, int hotHeadSize) =>
        new(RoutingTargets.Worker, RoutingSource.Classification, new RoutingMissContext(reason, hotHeadSize));

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
            logger.LogWarning(ex, "Router classification usage was not recorded for today");
        }
    }

    private sealed record ParsedClassification(string Target, string Reason);
}
