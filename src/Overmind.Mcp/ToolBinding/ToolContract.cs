using System.Text.Json;

namespace Overmind.Mcp.ToolBinding;

internal sealed record ToolContract(
    IReadOnlyDictionary<string, JsonElement> Parameters,
    IReadOnlySet<string> Required,
    bool Closed)
{
    public static ToolContract Open { get; } = new(
        new Dictionary<string, JsonElement>(),
        new HashSet<string>(StringComparer.Ordinal),
        Closed: false);
}
