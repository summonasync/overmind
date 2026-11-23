namespace Overmind.Mcp;

public static class McpProfileResolver
{
    public const string EnvironmentVariable = "OVERMIND_MCP_PROFILE";
    public const string ArgumentName = "--profile";
    public const string PlanningName = "planning";
    public const string JournalName = "journal";

    public static bool TryResolve(string[] args, out McpProfile profile, out string? error)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return TryAccept(fromEnvironment.Trim(), "the environment", out profile, out error);
        }

        var fromArguments = FindArgumentValue(args);
        if (fromArguments is not null)
        {
            return TryAccept(fromArguments, $"the {ArgumentName} argument", out profile, out error);
        }

        profile = McpProfile.Planning;
        error = null;
        return true;
    }

    public static string Describe(McpProfile profile) => profile switch
    {
        McpProfile.Journal => JournalName,
        _ => PlanningName
    };

    private static bool TryAccept(string candidate, string source, out McpProfile profile, out string? error)
    {
        if (candidate.Equals(PlanningName, StringComparison.OrdinalIgnoreCase))
        {
            profile = McpProfile.Planning;
            error = null;
            return true;
        }

        if (candidate.Equals(JournalName, StringComparison.OrdinalIgnoreCase))
        {
            profile = McpProfile.Journal;
            error = null;
            return true;
        }

        profile = McpProfile.Planning;
        error = $"[overmind-mcp] FATAL: UnknownMcpProfile '{candidate}' read from {source}. "
            + $"Valid profiles: {PlanningName}, {JournalName}. "
            + $"Fix: unset {EnvironmentVariable} for the default planning profile, or set it to "
            + $"{PlanningName} or {JournalName} (the {ArgumentName} argument works the same way).";
        return false;
    }

    private static string? FindArgumentValue(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(ArgumentName, StringComparison.Ordinal))
            {
                return i + 1 < args.Length ? args[i + 1] : null;
            }

            if (args[i].StartsWith($"{ArgumentName}=", StringComparison.Ordinal))
            {
                return args[i][(ArgumentName.Length + 1)..];
            }
        }

        return null;
    }
}
