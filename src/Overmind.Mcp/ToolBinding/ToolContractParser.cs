using System.Text.Json;

namespace Overmind.Mcp.ToolBinding;

internal static class ToolContractParser
{
    public static ToolContract Parse(JsonElement inputSchema)
    {
        if (inputSchema.ValueKind != JsonValueKind.Object)
        {
            return ToolContract.Open;
        }

        var parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var closed = false;
        if (inputSchema.TryGetProperty("properties", out var properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            closed = true;
            foreach (var property in properties.EnumerateObject())
            {
                parameters.Add(property.Name, property.Value.Clone());
            }
        }

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (inputSchema.TryGetProperty("required", out var requiredElement)
            && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var name in requiredElement.EnumerateArray())
            {
                if (name.ValueKind == JsonValueKind.String)
                {
                    required.Add(name.GetString()!);
                }
            }
        }

        return new ToolContract(parameters, required, closed);
    }
}
