using System.Reflection;

namespace Overmind.Host.Api;

public static class AdapterVersionCheck
{
    public const string MismatchWarning = "tools may be missing from your toolset; restart your MCP session";

    public const string PredatesWarning = "adapter predates version reporting; restart recommended";

    public static readonly string HostVersion = typeof(AdapterVersionCheck).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public static string? Warning(string? adapterVersion, string hostVersion) => adapterVersion switch
    {
        null => PredatesWarning,
        var version when string.Equals(version, hostVersion, StringComparison.Ordinal) => null,
        _ => MismatchWarning,
    };
}
