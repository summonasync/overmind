namespace Overmind.Mcp.Registration;

public static class ManagedServerKeys
{
    public const string Overmind = "overmind";
    public const string Journal = "overmind-journal";
    public const string Grimoire = "grimoire";
    public const string GrimoireCommand = "grimoire";

    public static IReadOnlyList<string> GrimoireArgs { get; } = ["mcp"];

    public static IReadOnlyDictionary<string, string> JournalEnvironment { get; } =
        new Dictionary<string, string>
        {
            [McpProfileResolver.EnvironmentVariable] = McpProfileResolver.JournalName,
        };

    public static string Name(ManagedServerKey key) => key switch
    {
        ManagedServerKey.Overmind => Overmind,
        ManagedServerKey.OvermindJournal => Journal,
        _ => Grimoire,
    };
}
