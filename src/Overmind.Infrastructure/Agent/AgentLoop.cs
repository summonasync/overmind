using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Llm;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentLoop(
    ILlmChatClientFactory chatFactory,
    IAgentPromptComposer prompts,
    IAgentToolInvoker tools,
    IAgentRunSettingsProvider settingsProvider,
    IAgentBudgetStore budget,
    IAgentGuardNotifier notifier,
    ConversationMessageStore auditTrail,
    EmbeddedAgentSessionRegistrar sessionRegistrar,
    ILogger<AgentLoop> logger)
{
    public const int MaxIterations = 12;
    public const string PerTurnCapCode = "perTurnTokenCap";
    public const string DailyBudgetCode = "dailyBudgetExhausted";
    public const string MaxIterationsCode = "maxIterations";

    public async IAsyncEnumerable<ConversationStreamEvent> RunAsync(
        Conversation conversation,
        IReadOnlyList<ConversationMessage> history,
        string userText,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await sessionRegistrar.EnsureRegisteredAsync(ct);

        var settings = await settingsProvider.GetAsync(ct);
        var client = await chatFactory.CreateAsync(ct);
        if (client is null)
        {
            yield return ConversationStreamEvent.Delta(LlmConversationStreamSource.ProviderMissingNotice);
            yield return ConversationStreamEvent.Completed();
            yield break;
        }

        var messages = new List<ChatMessage>(await prompts.ComposeAsync(conversation, history, userText, ct));
        var options = BuildOptions(tools);
        var usage = new AgentTurnUsage();
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyUsed = await budget.GetUsedAsync(day, ct);

        if (settings.DailyBudget > 0 && dailyUsed >= settings.DailyBudget)
        {
            yield return await DailyBudgetExhaustedAsync(conversation, settings, dailyUsed, ct);
            yield break;
        }

        for (var round = 1; ; round++)
        {
            if (round > MaxIterations)
            {
                var detail = $"The agent stopped after {MaxIterations} tool rounds (max iterations guard).";
                await notifier.NotifyGuardAsync("Agent turn stopped by a guard", detail, conversation.ProjectId, ct);
                yield return ConversationStreamEvent.Failed(detail, MaxIterationsCode);
                yield break;
            }

            var roundResult = new AgentRoundResult();
            await foreach (var frame in StreamRoundAsync(client, messages, options, usage, roundResult, ct))
            {
                yield return frame;
            }

            if (settings.PerTurnMaxTokens > 0 && usage.Total > settings.PerTurnMaxTokens)
            {
                var detail =
                    $"Per-turn token cap exceeded: {usage.Total} tokens used, the cap is {settings.PerTurnMaxTokens}.";
                await notifier.NotifyGuardAsync("Agent turn stopped by a guard", detail, conversation.ProjectId, ct);
                yield return ConversationStreamEvent.Failed(detail, PerTurnCapCode);
                yield break;
            }

            dailyUsed = await budget.AddUsageAsync(day, usage.Total - roundResult.UsageBeforeRound, ct);
            if (settings.DailyBudget > 0 && dailyUsed >= settings.DailyBudget)
            {
                yield return await DailyBudgetExhaustedAsync(conversation, settings, dailyUsed, ct);
                yield break;
            }

            if (roundResult.Calls.Count == 0)
            {
                yield return ConversationStreamEvent.Completed(usage.Total);
                yield break;
            }

            foreach (var call in roundResult.Calls)
            {
                yield return ConversationStreamEvent.ToolCall(call.Name);
            }

            messages.Add(BuildAssistantMessage(roundResult));
            foreach (var toolMessage in await RunCallsAsync(conversation, roundResult.Calls, ct))
            {
                messages.Add(toolMessage);
            }
        }
    }

    private static ChatOptions BuildOptions(IAgentToolInvoker tools) => new()
    {
        ToolMode = ChatToolMode.Auto,
        Tools = [.. tools.CreateTools()]
    };

    private async Task<ConversationStreamEvent> DailyBudgetExhaustedAsync(
        Conversation conversation,
        AgentRunSettings settings,
        long dailyUsed,
        CancellationToken ct)
    {
        var detail = $"Daily budget exhausted: {dailyUsed} of {settings.DailyBudget} tokens used today.";
        await notifier.NotifyGuardAsync("Agent daily budget exhausted", detail, conversation.ProjectId, ct);
        return ConversationStreamEvent.Failed(detail, DailyBudgetCode);
    }

    private async IAsyncEnumerable<ConversationStreamEvent> StreamRoundAsync(
        IChatClient client,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        AgentTurnUsage usage,
        AgentRoundResult roundResult,
        [EnumeratorCancellation] CancellationToken ct)
    {
        roundResult.UsageBeforeRound = usage.Total;
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct))
        {
            if (update.Text.Length > 0)
            {
                roundResult.Text.Append(update.Text);
                yield return ConversationStreamEvent.Delta(update.Text);
            }

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call:
                        roundResult.Calls.Add(call);
                        break;
                    case UsageContent usageUpdate:
                        usage.Add(usageUpdate.Details);
                        break;
                }
            }
        }
    }

    private async Task<IReadOnlyList<ChatMessage>> RunCallsAsync(
        Conversation conversation,
        IReadOnlyList<FunctionCallContent> calls,
        CancellationToken ct)
    {
        var results = new List<ChatMessage>();
        foreach (var call in calls)
        {
            var arguments = call.Arguments is { } raw
                ? JsonSerializer.SerializeToElement(raw, AgentLoopbackClient.JsonOptions)
                : JsonSerializer.SerializeToElement(new { }, AgentLoopbackClient.JsonOptions);

            var label = JsonSerializer.Serialize(new { tool = call.Name }, AgentLoopbackClient.JsonOptions);
            await AppendAuditAsync(conversation.Id, label, ct);

            var result = await tools.InvokeAsync(call.Name, arguments, ct);
            results.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]));
        }

        return results;
    }

    private async Task AppendAuditAsync(Guid conversationId, string label, CancellationToken ct)
    {
        try
        {
            await auditTrail.AppendAsync(conversationId, ConversationMessage.ToolRole, label, ct);
        }
        catch (PlanningException ex)
        {
            logger.LogWarning(ex, "Tool audit row was not persisted for conversation {ConversationId}", conversationId);
        }
    }

    private static ChatMessage BuildAssistantMessage(AgentRoundResult roundResult)
    {
        var contents = new List<AIContent>();
        if (roundResult.Text.Length > 0)
        {
            contents.Add(new TextContent(roundResult.Text.ToString()));
        }

        contents.AddRange(roundResult.Calls);
        return new ChatMessage(ChatRole.Assistant, contents);
    }

    private sealed class AgentRoundResult
    {
        public StringBuilder Text { get; } = new();
        public List<FunctionCallContent> Calls { get; } = [];
        public long UsageBeforeRound { get; set; }
    }

    private sealed class AgentTurnUsage
    {
        public long Total { get; private set; }

        public void Add(UsageDetails usage)
        {
            Total += usage.TotalTokenCount ?? (usage.InputTokenCount ?? 0) + (usage.OutputTokenCount ?? 0);
        }
    }
}
