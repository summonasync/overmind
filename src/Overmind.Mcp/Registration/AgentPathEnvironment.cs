namespace Overmind.Mcp.Registration;

public sealed record AgentPathEnvironment
{
    public required string HomeDirectory { get; init; }
    public required IReadOnlyDictionary<string, string> Variables { get; init; }

    public static AgentPathEnvironment Current()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                variables[key] = value;
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = variables.TryGetValue("HOME", out var unixHome) ? unixHome : string.Empty;
        }

        return new AgentPathEnvironment { HomeDirectory = home, Variables = variables };
    }
}
