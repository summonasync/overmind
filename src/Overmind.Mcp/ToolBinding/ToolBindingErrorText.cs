using System.Text.Json;

namespace Overmind.Mcp.ToolBinding;

internal static class ToolBindingErrorText
{
    public static string Render(string toolName, ToolBindingViolation violation, ToolContract contract)
    {
        var expected = ExpectedParameters(contract);
        var tail = expected.Length == 0
            ? " The tool takes no parameters."
            : $" Expected parameters: {expected}.";
        return $"Invalid parameters for tool '{toolName}': {violation.Parameter} {violation.Problem}.{tail}";
    }

    private static string ExpectedParameters(ToolContract contract) => string.Join(
        ", ",
        contract.Parameters.Select(parameter =>
            Describe(parameter.Key, parameter.Value)
            + (contract.Required.Contains(parameter.Key) ? " [required]" : string.Empty)));

    private static string Describe(string name, JsonElement schema)
    {
        var types = SchemaTypes(schema);
        var shape = types.Count == 0 ? "any" : string.Join(" or ", types);
        if (schema.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String)
        {
            shape = $"{shape} ({format.GetString()})";
        }

        if (schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var options = enumValues.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString());
            var joined = string.Join(" | ", options);
            if (joined.Length > 0)
            {
                shape = $"{shape}: {joined}";
            }
        }

        return $"{name} ({shape})";
    }

    private static IReadOnlyList<string> SchemaTypes(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type))
        {
            return [];
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => [type.GetString()!],
            JsonValueKind.Array => [.. type.EnumerateArray()
                .Where(t => t.ValueKind == JsonValueKind.String)
                .Select(t => t.GetString()!)],
            _ => [],
        };
    }
}
