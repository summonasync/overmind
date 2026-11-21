using Overmind.Core.Agents;

namespace Overmind.Mcp.Registration;

public static class AgentInstructionFile
{
    public static string ResolvePath(AgentDefinition agent, string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        if (agent.AgentFileCandidates.Count == 0)
        {
            throw new InvalidOperationException($"agent '{agent.Name}' has no agent file candidates.");
        }

        foreach (var candidate in agent.AgentFileCandidates)
        {
            var path = Path.Combine(repoRoot, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(repoRoot, agent.AgentFileCandidates[0]);
    }
}
