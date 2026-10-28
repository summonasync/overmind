namespace Overmind.Mcp;

public static class AgentIdentity
{
    public const string PlaceholderAlias = "agent";
    private const string DefaultVendor = "unknown";

    public static string ResolveAlias() => Resolve("OVERMIND_ALIAS", PlaceholderAlias);

    public static string ResolveVendor() => Resolve("OVERMIND_VENDOR", DefaultVendor);

    private static string Resolve(string environmentVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
