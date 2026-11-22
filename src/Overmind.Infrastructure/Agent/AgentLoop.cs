using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Agent.Tools;
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
    IAgentDelegationGate delegationGate,
    AgentRouteRecorder routeRecorder,
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
        [EnumeratorCancellation] CancellationToken ct = default,
        AgentRole role = AgentRole.Worker,
        RoutingMissContext? missContext = null)
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

        var messages = new List<ChatMessage>(
            await prompts.ComposeAsync(conversation, ConversationContextWindow.Apply(history), userText, ct));
        if (missContext is { } miss && messages.Count > 0)
        {
            messages[0] = new ChatMessage(ChatRole.System, $"{messages[0].Text}\n\n{miss.Describe()}");
        }
        var delegationArmed = await delegationGate.IsEnabledAsync(conversation.ProjectId, ct);
        Guid? instructionMessageId = delegationArmed
            ? (await auditTrail.LatestAsync(conversation.Id, ConversationMessage.UserRole, ct))?.Id
            : null;
        var options = BuildOptions(tools, delegationArmed);
        logger.LogInformation(
            "Agent turn for conversation {ConversationId} composed {SystemPromptChars} system prompt chars, {HistoryChars} history chars, {ToolSchemaChars} tool schema chars, {Role} role",
            conversation.Id,
            SystemPromptChars(messages),
            HistoryChars(messages),
            ToolSchemaChars(options.Tools),
            RoleTag(role));
        var invocation = new AgentToolInvocation(
            delegationArmed, instructionMessageId, conversation.Id, conversation.ProjectId);
        var usage = new AgentTurnUsage();
        var day = AgentBudgetDay.Current();
        var dailyUsed = await budget.GetUsedAsync(day, ct);

        if (settings.DailyBudget > 0 && dailyUsed >= settings.DailyBudget)
        {
            yield return await DailyBudgetExhaustedAsync(conversation, settings, dailyUsed, ct);
            yield break;
        }

        var workerTurn = role == AgentRole.Worker;
        if (workerTurn)
        {
            routeRecorder.BeginTurn(conversation.Id);
        }

        try
        {
            await foreach (var frame in RunRoundsAsync(
                conversation, client, messages, options, invocation, usage, settings, day, dailyUsed, role, ct))
            {
                yield return frame;
            }
        }
        finally
        {
            if (workerTurn)
            {
                await RecordRouteFallbackAsync(conversation, userText, ct);
                routeRecorder.EndTurn(conversation.Id);
            }
        }
    }

    private async IAsyncEnumerable<ConversationStreamEvent> RunRoundsAsync(
        Conversation conversation,
        IChatClient client,
        List<ChatMessage> messages,
        ChatOptions options,
        AgentToolInvocation invocation,
        AgentTurnUsage usage,
        AgentRunSettings settings,
        DateOnly day,
        long dailyUsed,
        AgentRole role,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var round = 1; ; round++)
        {
            if (round > MaxIterations)
            {
                var detail = $"The agent stopped after {MaxIterations} tool rounds (max iterations guard).";
                await NotifyGuardAsync("Agent turn stopped by a guard", detail, conversation.ProjectId, ct);
                yield return ConversationStreamEvent.Failed(detail, MaxIterationsCode);
                yield break;
            }

            var roundResult = new AgentRoundResult();
            await foreach (var frame in StreamRoundAsync(client, messages, options, usage, roundResult, ct))
            {
                yield return frame;
            }

            var roundTotal = usage.Total - roundResult.TotalUsageBeforeOutput;
            var roundOutput = usage.Output - roundResult.UsageBeforeOutput;
            logger.LogInformation(
                "Agent round {Round} of conversation {ConversationId} used {InputTokens} input, {OutputTokens} output and {TotalTokens} total tokens, {Role} role",
                round,
                conversation.Id,
                roundTotal - roundOutput,
                roundOutput,
                roundTotal,
                RoleTag(role));

            dailyUsed = await budget.AddUsageAsync(day, usage.Output - roundResult.UsageBeforeOutput, ct);

            if (settings.PerTurnMaxTokens > 0 && usage.Total > settings.PerTurnMaxTokens)
            {
                var detail =
                    $"Per-turn token cap exceeded: {usage.Total} tokens used, the cap is {settings.PerTurnMaxTokens}.";
                await NotifyGuardAsync("Agent turn stopped by a guard", detail, conversation.ProjectId, ct);
                yield return ConversationStreamEvent.Failed(detail, PerTurnCapCode);
                yield break;
            }

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

            if (roundResult.Calls.FirstOrDefault(IsBounceCall) is { } bounceCall)
            {
                yield return ConversationStreamEvent.ToolCall(bounceCall.Name);
                await AppendAuditAsync(conversation.Id, ToolLabel(bounceCall.Name, conversation.ProjectId), ct);
                var reason = BounceReason(bounceCall);
                logger.LogInformation(
                    "Agent turn for conversation {ConversationId} ended with a {Role} bounce, reason {BounceReason}",
                    conversation.Id,
                    RoleTag(role),
                    reason);
                yield return ConversationStreamEvent.Bounced(reason);
                yield return ConversationStreamEvent.Completed(usage.Total);
                yield break;
            }

            foreach (var call in roundResult.Calls)
            {
                yield return ConversationStreamEvent.ToolCall(call.Name);
            }

            messages.Add(BuildAssistantMessage(roundResult));
            foreach (var toolMessage in await RunCallsAsync(conversation, roundResult.Calls, invocation, ct))
            {
                messages.Add(toolMessage);
            }
        }
    }

    private static ChatOptions BuildOptions(IAgentToolInvoker tools, bool includeHumanGated) => new()
    {
        ToolMode = ChatToolMode.Auto,
        Tools = [.. tools.CreateTools(includeHumanGated)]
    };

    private static int SystemPromptChars(IReadOnlyList<ChatMessage> messages) =>
        messages.Count > 0 && messages[0].Role == ChatRole.System ? messages[0].Text.Length : 0;

    private static int HistoryChars(IReadOnlyList<ChatMessage> messages) =>
        messages.Skip(1).Take(messages.Count - 2).Sum(message => message.Text.Length);

    private static int ToolSchemaChars(IList<AITool> tools) =>
        tools.Sum(tool => tool.Name.Length + tool.Description.Length +
            (tool is AIFunction function ? function.JsonSchema.GetRawText().Length : 0));

    private static string RoleTag(AgentRole role) => role.ToString().ToLowerInvariant();

    private static bool IsBounceCall(FunctionCallContent call) => call.Name == RouteAgentTools.BounceToolName;

    private static string ToolLabel(string toolName, Guid? projectId)
    {
        var label = new Dictionary<string, object> { ["tool"] = toolName };
        if (projectId is null)
        {
            label["globalScope"] = true;
        }

        return JsonSerializer.Serialize(label, AgentLoopbackClient.JsonOptions);
    }

    private static string BounceReason(FunctionCallContent call)
    {
        if (call.Arguments is not { } arguments ||
            !arguments.TryGetValue("reason", out var value))
        {
            return string.Empty;
        }

        return value switch
        {
            string text => AgentRouteText.Clamp(text, AgentRouteText.MaxMissReasonLength),
            JsonElement { ValueKind: JsonValueKind.String } element =>
                AgentRouteText.Clamp(element.GetString(), AgentRouteText.MaxMissReasonLength),
            _ => string.Empty
        };
    }

    private async Task RecordRouteFallbackAsync(Conversation conversation, string userText, CancellationToken ct)
    {
        try
        {
            await routeRecorder.RecordUnanalyzedAsync(conversation.Id, userText, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Route fallback row was not recorded for conversation {ConversationId}",
                conversation.Id);
        }
    }

    private async Task NotifyGuardAsync(string title, string detail, Guid? projectId, CancellationToken ct)
    {
        try
        {
            await notifier.NotifyGuardAsync(title, detail, projectId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Guard notification was not delivered for project {ProjectId}", projectId);
        }
    }

    private async Task<ConversationStreamEvent> DailyBudgetExhaustedAsync(
        Conversation conversation,
        AgentRunSettings settings,
        long dailyUsed,
        CancellationToken ct)
    {
        var detail = $"Daily budget exhausted: {dailyUsed} of {settings.DailyBudget} tokens used today.";
        await NotifyGuardAsync("Agent daily budget exhausted", detail, conversation.ProjectId, ct);
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
        roundResult.UsageBeforeOutput = usage.Output;
        roundResult.TotalUsageBeforeOutput = usage.Total;
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
        AgentToolInvocation invocation,
        CancellationToken ct)
    {
        var results = new List<ChatMessage>();
        foreach (var call in calls)
        {
            var arguments = call.Arguments is { } raw
                ? JsonSerializer.SerializeToElement(raw, AgentLoopbackClient.JsonOptions)
                : JsonSerializer.SerializeToElement(new { }, AgentLoopbackClient.JsonOptions);

            var label = ToolLabel(call.Name, conversation.ProjectId);
            await AppendAuditAsync(conversation.Id, label, ct);

            var result = await tools.InvokeAsync(call.Name, arguments, invocation, ct);
            if (result.ValueKind == JsonValueKind.Object &&
                result.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                await AppendAuditAsync(conversation.Id, RefusalLabel(call.Name, result), ct);
            }

            results.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]));
        }

        return results;
    }

    // The pre-call row carries only the tool name, so a refusal was invisible in the trail and
    // evidence-gathering meant coaxing the model into repeating it (13192d5e). A refused call
    // gets a second row with the bounded refusal — the audit record for the human, not an echo
    // to the model (review-241-02's rule governs model-facing text, not persisted rows).
    private const int RefusalExcerptLimit = 1000;

    private static string RefusalLabel(string toolName, JsonElement result)
    {
        var message = result.GetProperty("error").GetString();
        if (message is { Length: > RefusalExcerptLimit })
        {
            message = message[..RefusalExcerptLimit];
        }

        var label = new Dictionary<string, object>
        {
            ["tool"] = toolName,
            ["refusal"] = message ?? string.Empty
        };
        if (result.TryGetProperty("code", out var code) &&
            code.ValueKind == JsonValueKind.String)
        {
            label["code"] = code.GetString() ?? string.Empty;
        }

        return JsonSerializer.Serialize(label, AgentLoopbackClient.JsonOptions);
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
        public long UsageBeforeOutput { get; set; }
        public long TotalUsageBeforeOutput { get; set; }
    }

    private sealed class AgentTurnUsage
    {
        public long Total { get; private set; }

        public long Output { get; private set; }

        public void Add(UsageDetails usage)
        {
            Total += usage.TotalTokenCount ?? (usage.InputTokenCount ?? 0) + (usage.OutputTokenCount ?? 0);
            Output += usage.OutputTokenCount ?? usage.TotalTokenCount ?? usage.InputTokenCount ?? 0;
        }
    }
}
