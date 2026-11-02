using System.Text.Json;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Agent.Tools;

namespace Overmind.Infrastructure.Agent;

public sealed class AgentToolRegistry : IAgentToolInvoker
{
    public const string HumanGatedToolMessage =
        "'{0}' is human-gated: validations and done flips are not callable by the loop until delegated approvals land.";

    private readonly Dictionary<string, AgentToolSpec> _specs;
    private readonly IReadOnlyList<AITool> _tools;

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
    }

    public int Count => _specs.Count;

    public IReadOnlyList<AITool> CreateTools() => _tools;

    public async Task<JsonElement> InvokeAsync(string toolName, JsonElement arguments, CancellationToken ct)
    {
        var name = toolName.Trim();
        if (!_specs.TryGetValue(name, out var spec))
        {
            return Error($"Unknown tool '{toolName}'. Known tools: {string.Join(", ", OrderedNames())}.");
        }

        if (spec.Category == AgentToolCategory.HumanGated)
        {
            return Error(string.Format(HumanGatedToolMessage, name));
        }

        try
        {
            return await spec.Invoke(arguments, ct);
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

    private IEnumerable<string> OrderedNames() => _specs.Keys.OrderBy(name => name, StringComparer.Ordinal);

    private static JsonElement Error(string message) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { error = message })).RootElement.Clone();
}
