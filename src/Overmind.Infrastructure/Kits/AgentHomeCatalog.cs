using Overmind.Core;
using Overmind.Core.Agents;

namespace Overmind.Infrastructure.Kits;

// Bridges the Mcp-side agent knowledge (AgentRegistry) into the home-deploy door's
// AgentHomeSpec shape. The platform mapping relies on every registry name turning
// into its enum member once the dash is dropped (kimi-code → kimicode → KimiCode).
public static class AgentHomeCatalog
{
    public static IReadOnlyList<AgentHomeSpec> Build() =>
        AgentRegistry.All
            .Select(agent => new AgentHomeSpec(
                agent.Name,
                MapPlatform(agent.Name),
                agent.ConfigDirCandidates.ToArray(),
                agent.AgentFileCandidates.FirstOrDefault()
                    ?? throw new InvalidOperationException($"Agent '{agent.Name}' declares no entry-file candidate"))
            )
            .ToList();

    private static AgentPlatform MapPlatform(string agentName)
    {
        var candidate = agentName.Replace("-", string.Empty);
        if (Enum.TryParse<AgentPlatform>(candidate, ignoreCase: true, out var platform))
        {
            return platform;
        }

        throw new InvalidOperationException($"No AgentPlatform mapping for agent '{agentName}'");
    }
}
