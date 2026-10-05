using System.Reflection;

namespace Overmind.Mcp;

public static class AdapterVersion
{
    public const string HeaderName = "X-Overmind-Adapter-Version";

    public static readonly string Current = typeof(AdapterVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
