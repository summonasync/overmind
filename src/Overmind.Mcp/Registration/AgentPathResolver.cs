using Overmind.Core.Agents;

namespace Overmind.Mcp.Registration;

public static class AgentPathResolver
{
    public static string ResolveUserConfigPath(AgentDefinition agent, AgentPathEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(environment);

        if (agent.UserConfigEnvironmentVariable is { } variableName)
        {
            var fileName = agent.UserConfigEnvironmentFileName
                ?? throw new InvalidOperationException($"Agent '{agent.Name}' declares an env override variable without a file name.");
            if (environment.Variables.TryGetValue(variableName, out var overrideRoot) && !string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.Combine(overrideRoot, fileName);
            }
        }

        return ExpandPath(agent.DefaultUserConfigTemplate, environment)
            ?? throw new InvalidOperationException($"Agent '{agent.Name}' default config template could not be expanded.");
    }

    public static string ResolveProjectConfigPath(AgentDefinition agent, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        return Path.Combine(projectRoot, agent.ProjectRelativePath);
    }

    public static string? ExpandPath(string template, AgentPathEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(environment);

        if (template == "~")
        {
            return environment.HomeDirectory;
        }

        if (template.StartsWith("~/", StringComparison.Ordinal) || template.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(environment.HomeDirectory, template[2..]);
        }

        if (TryExpandLeadingVariable(template, environment, out var expanded))
        {
            return expanded;
        }

        return template;
    }

    private static bool TryExpandLeadingVariable(string template, AgentPathEnvironment environment, out string? expanded)
    {
        expanded = null;

        if (template.StartsWith('$'))
        {
            var separatorIndex = template.IndexOfAny(['/', '\\']);
            var variableName = separatorIndex < 0 ? template[1..] : template[1..separatorIndex];
            if (variableName.Length == 0)
            {
                return false;
            }

            if (!environment.Variables.TryGetValue(variableName, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            expanded = separatorIndex < 0 ? value : Path.Combine(value, template[(separatorIndex + 1)..]);
            return true;
        }

        if (template.StartsWith('%'))
        {
            var closingIndex = template.IndexOf('%', 1);
            if (closingIndex <= 1)
            {
                return false;
            }

            var variableName = template[1..closingIndex];
            if (!environment.Variables.TryGetValue(variableName, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var remainder = template[(closingIndex + 1)..];
            expanded = remainder.StartsWith('/') || remainder.StartsWith('\\') ? Path.Combine(value, remainder[1..]) : value + remainder;
            return true;
        }

        return false;
    }
}
