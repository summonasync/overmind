namespace Overmind.Mcp;

public static class AgentIdentity
{
    private const string DefaultAlias = "agent";
    private const string DefaultVendor = "unknown";

    public static string ResolveAlias() => Resolve("OVERMIND_ALIAS", DefaultAlias);

    public static string ResolveVendor() => Resolve("OVERMIND_VENDOR", DefaultVendor);

    private static string Resolve(string environmentVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
