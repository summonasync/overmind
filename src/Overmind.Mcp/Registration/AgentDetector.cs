using Overmind.Core.Agents;

namespace Overmind.Mcp.Registration;

public static class AgentDetector
{
    public static bool IsDetected(AgentDefinition agent, AgentPathEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(environment);

        return HasConfigCandidate(agent, environment) || HasBinaryOnPath(agent, environment);
    }

    private static bool HasConfigCandidate(AgentDefinition agent, AgentPathEnvironment environment)
    {
        foreach (var candidate in agent.ConfigDirCandidates)
        {
            var path = AgentPathResolver.ExpandPath(candidate, environment);
            if (path is not null && Path.Exists(path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBinaryOnPath(AgentDefinition agent, AgentPathEnvironment environment)
    {
        if (!environment.Variables.TryGetValue("PATH", out var pathVariable) || string.IsNullOrWhiteSpace(pathVariable))
        {
            return false;
        }

        var directories = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var directory in directories)
        {
            foreach (var binaryName in agent.BinaryNames)
            {
                if (BinaryExists(directory, binaryName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool BinaryExists(string directory, string binaryName)
    {
        if (File.Exists(Path.Combine(directory, binaryName)))
        {
            return true;
        }

        return OperatingSystem.IsWindows() && File.Exists(Path.Combine(directory, binaryName + ".exe"));
    }
}
