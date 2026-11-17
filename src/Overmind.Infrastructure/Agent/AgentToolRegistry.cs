using System.Text.Json;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Agent.Routes;
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

    public const string ProjectScopeMismatchCode = "projectScopeMismatch";

    public const string ProjectScopeMismatchMessage =
        "'{0}' is project-scoped: this conversation belongs to project '{1}', but the projectId this call carries " +
        "names project '{2}'. The host compared that argument with the conversation and refused this call instead of " +
        "redirecting it, so nothing was read or written. Call '{0}' with projectId={1}. If this conversation really " +
        "belongs to another project, say so to the owner.";

    public const string ProjectScopeUnverifiableCode = "projectScopeUnverifiable";

    public const string ProjectScopeUnverifiableMessage =
        "'{0}' is project-scoped and this conversation belongs to project '{1}', but the projectId this call carries " +
        "cannot be read as a project id, so the host cannot tell which board the call meant and refused it. Nothing " +
        "was read or written. Call '{0}' with projectId={1}, or omit projectId when the tool says it is optional.";

    private readonly Dictionary<string, AgentToolSpec> _specs;
    private readonly IReadOnlyList<AITool> _tools;
    private readonly IReadOnlyList<AITool> _toolsWithHumanGated;

    public AgentToolRegistry(
        AgentLoopbackClient api,
        EmbeddedAgentSessionState sessionState,
        IGrimoireMemoryClient memory,
        ConversationMessageStore messages,
        AgentRouteRecorder routeRecorder)
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
            .Concat(RouteAgentTools.Build(routeRecorder, messages))
            .ToList();

        _specs = specs
            .GroupBy(spec => spec.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        _tools = [.. _specs.Values
            .Where(spec => spec.Category == AgentToolCategory.Standard)
            .Select(spec => new AgentToolFunction(spec))];
        _toolsWithHumanGated = [.. _specs.Values.Select(spec => new AgentToolFunction(spec))];
        Specs = [.. _specs.Values.OrderBy(spec => spec.Name, StringComparer.Ordinal)];
    }

    public int Count => _specs.Count;

    public IReadOnlyList<AgentToolSpec> Specs { get; }

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

        if (ScopeRefusalFor(spec, arguments, invocation) is { } scopeRefusal)
        {
            return scopeRefusal;
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

    // The scope comparison lives here, once, for all seven families — no handler sees it.
    // Specs marked AgentToolScope.CrossProject are exempt from this projectId check; task 2 of 241
    // owes them the authorProjectId == invocation.ProjectId check. issues_create and issues_append are
    // deliberately unmarked until then, so a foreign target board is refused by this comparison today.
    private static JsonElement? ScopeRefusalFor(
        AgentToolSpec spec, JsonElement arguments, AgentToolInvocation invocation)
    {
        if (spec.Scope != AgentToolScope.Enforce || invocation.ProjectId is not { } own ||
            arguments.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!arguments.TryGetProperty("projectId", out var addressed) || addressed.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (AddressedProject(addressed) is not { } sent)
        {
            return ScopedError(
                string.Format(ProjectScopeUnverifiableMessage, spec.Name, own), ProjectScopeUnverifiableCode);
        }

        return sent == own
            ? null
            : ScopedError(
                string.Format(ProjectScopeMismatchMessage, spec.Name, own, sent), ProjectScopeMismatchCode);
    }

    private static Guid? AddressedProject(JsonElement addressed) =>
        addressed.ValueKind == JsonValueKind.String && Guid.TryParse(addressed.GetString(), out var parsed)
            ? parsed
            : null;

    private IEnumerable<string> OrderedNames() => _specs.Keys.OrderBy(name => name, StringComparer.Ordinal);

    private static JsonElement Error(string message) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { error = message })).RootElement.Clone();

    private static JsonElement ScopedError(string message, string code) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { error = message, code })).RootElement.Clone();
}
