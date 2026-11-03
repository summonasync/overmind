using System.Text.Json;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Agent.Tools;
using Overmind.Infrastructure.Grimoire;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentToolRegistry : IAgentToolInvoker
{
    public const string HumanGatedToolMessage =
        "'{0}' is human-gated: delegated approvals are off for this project, so the loop cannot call it. " +
        "Ask the owner to approve via the UI or reply to the approval notification.";

    public const string DelegationInstructionMissingMessage =
        "'{0}' is human-gated and delegated approvals are on, but this turn carries no owner instruction " +
        "to cite — the owner must instruct the delegation in the conversation first.";

    public const string DelegatedDispatchMissingMessage =
        "'{0}' has no delegated dispatch path and cannot be called under delegated approvals.";

    public const string ConversationScopeMissingMessage =
        "'{0}' works on the current conversation, but this turn carries no conversation scope.";

    private readonly Dictionary<string, AgentToolSpec> _specs;
    private readonly IReadOnlyList<AITool> _tools;
    private readonly IReadOnlyList<AITool> _toolsWithHumanGated;

    public AgentToolRegistry(
        AgentLoopbackClient api,
        EmbeddedAgentSessionState sessionState,
        IGrimoireMemoryClient memory,
        ConversationMessageStore messages)
    {
        var specs = PlanningAgentTools.Build(api)
            .Concat(EntryAgentTools.Build(api))
            .Concat(IssueAgentTools.Build(api))
            .Concat(LaneAgentTools.Build(api))
            .Concat(WaveAgentTools.Build(api, sessionState))
            .Concat(KitAgentTools.Build(api))
            .Concat(BoardAgentTools.Build(api))
            .Concat(ContextAgentTools.Build(api, memory))
            .Concat(MemoryAgentTools.Build(memory))
            .Concat(ConversationHistoryAgentTools.Build(messages))
            .ToList();

        _specs = specs
            .GroupBy(spec => spec.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _tools = [.. _specs.Values
            .Where(spec => spec.Category == AgentToolCategory.Standard)
            .Select(spec => new AgentToolFunction(spec))];
        _toolsWithHumanGated = [.. _specs.Values.Select(spec => new AgentToolFunction(spec))];
    }

    public int Count => _specs.Count;

    public IReadOnlyList<AITool> CreateTools(bool includeHumanGated) =>
        includeHumanGated ? _toolsWithHumanGated : _tools;

    public async Task<JsonElement> InvokeAsync(
        string toolName, JsonElement arguments, AgentToolInvocation invocation, CancellationToken ct)
    {
        var name = toolName.Trim();
        if (!_specs.TryGetValue(name, out var spec))
        {
            return Error($"Unknown tool '{toolName}'. Known tools: {string.Join(", ", OrderedNames())}.");
        }

        if (RefusalFor(spec, invocation) is { } refusal)
        {
            return Error(refusal);
        }

        try
        {
            if (invocation.DelegationArmed &&
                invocation.InstructionMessageId is { } instructionId &&
                spec.DelegatedInvoke is { } delegated)
            {
                return await delegated(arguments, instructionId, ct);
            }

            return spec.ConversationInvoke is { } conversationScoped
                ? invocation.ConversationId is { } conversationId
                    ? await conversationScoped(arguments, conversationId, ct)
                    : Error(string.Format(ConversationScopeMissingMessage, name))
                : await spec.Invoke(arguments, ct);
        }
        catch (LoopbackApiException ex)
        {
            return Error(ex.Message);
        }
        catch (GrimoireMemoryException ex)
        {
            return Error(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Error(ex.Message);
        }
        catch (JsonException ex)
        {
            return Error($"Tool '{name}' received an unusable response payload: {ex.Message}");
        }
    }

    private static string? RefusalFor(AgentToolSpec spec, AgentToolInvocation invocation) =>
        spec.Category switch
        {
            AgentToolCategory.HumanGated when !invocation.DelegationArmed =>
                string.Format(HumanGatedToolMessage, spec.Name),
            AgentToolCategory.HumanGated when invocation.InstructionMessageId is null =>
                string.Format(DelegationInstructionMissingMessage, spec.Name),
            AgentToolCategory.HumanGated when spec.DelegatedInvoke is null =>
                string.Format(DelegatedDispatchMissingMessage, spec.Name),
            _ => null
        };

    private IEnumerable<string> OrderedNames() => _specs.Keys.OrderBy(name => name, StringComparer.Ordinal);

    private static JsonElement Error(string message) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { error = message })).RootElement.Clone();
}
