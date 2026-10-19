namespace Overmind.Mcp.Registration;

public static class ManagedServerKeys
{
    public const string Overmind = "overmind";
    public const string Grimoire = "grimoire";
    public const string GrimoireCommand = "grimoire";

    public static IReadOnlyList<string> GrimoireArgs { get; } = ["mcp"];

    public static string Name(ManagedServerKey key)
    {
        return key == ManagedServerKey.Overmind ? Overmind : Grimoire;
    }
}
