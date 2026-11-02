using System.Text.Json;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Agent.Tools;

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

    private readonly Dictionary<string, AgentToolSpec> _specs;
    private readonly IReadOnlyList<AITool> _tools;
    private readonly IReadOnlyList<AITool> _toolsWithHumanGated;

    public AgentToolRegistry(
        AgentLoopbackClient api,
        EmbeddedAgentSessionState sessionState)
    {
        var specs = PlanningAgentTools.Build(api)
            .Concat(EntryAgentTools.Build(api))
            .Concat(IssueAgentTools.Build(api))
            .Concat(LaneAgentTools.Build(api))
            .Concat(WaveAgentTools.Build(api, sessionState))
            .Concat(KitAgentTools.Build(api))
            .Concat(BoardAgentTools.Build(api))
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
            return invocation.DelegationArmed &&
                   invocation.InstructionMessageId is { } instructionId &&
                   spec.DelegatedInvoke is { } delegated
                ? await delegated(arguments, instructionId, ct)
                : await spec.Invoke(arguments, ct);
        }
        catch (LoopbackApiException ex)
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
