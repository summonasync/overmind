using System.Text.Json;

namespace Overmind.Mcp.ToolBinding;

internal static class ToolBindingValidator
{
    public static ToolBindingViolation? Find(
        IDictionary<string, JsonElement>? arguments, ToolContract contract)
    {
        if (arguments is not null)
        {
            foreach (var name in arguments.Keys)
            {
                if (contract.Closed && !contract.Parameters.ContainsKey(name))
                {
                    var near = NearestName.Suggest(name, contract.Parameters.Keys);
                    return new ToolBindingViolation(name, near is null
                        ? "is not a parameter of this tool"
                        : $"is not a parameter of this tool; did you mean '{near}'?");
                }
            }

            foreach (var (name, value) in arguments)
            {
                if (!contract.Parameters.TryGetValue(name, out var schema))
                {
                    continue;
                }

                var violation = MatchValue(name, value, schema);
                if (violation is not null)
                {
                    return violation;
                }
            }
        }

        foreach (var name in contract.Required)
        {
            if (arguments is null || !arguments.ContainsKey(name))
            {
                var mistaken = arguments is null || arguments.Count == 0
                    ? null
                    : NearestName.Suggest(name, arguments.Keys.Where(key => !contract.Required.Contains(key)));
                return new ToolBindingViolation(name, mistaken is null
                    ? "is required but was not provided"
                    : $"is required but was not provided — the call carries '{mistaken}' instead; " +
                      $"put the value under '{name}'");
            }
        }

        return null;
    }

    private static ToolBindingViolation? MatchValue(string name, JsonElement value, JsonElement schema)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var types = SchemaTypes(schema);
        if (types.Count == 0)
        {
            return null;
        }

        var kind = value.ValueKind;
        var matches = types.Any(type => type switch
        {
            "string" => kind is JsonValueKind.String,
            "integer" or "number" => kind is JsonValueKind.Number,
            "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
            "array" => kind is JsonValueKind.Array,
            "object" => kind is JsonValueKind.Object,
            _ => true,
        });
        if (!matches)
        {
            return new ToolBindingViolation(
                name, $"expects {DescribeType(schema)}, received {kind.ToString().ToLowerInvariant()}");
        }

        return MatchFormat(name, value, schema, types);
    }

    private static ToolBindingViolation? MatchFormat(
        string name, JsonElement value, JsonElement schema, IReadOnlyList<string> types)
    {
        if (schema.TryGetProperty("format", out var format)
            && format.ValueKind == JsonValueKind.String
            && format.GetString() == "uuid"
            && value.ValueKind == JsonValueKind.String
            && !Guid.TryParse(value.GetString(), out _))
        {
            return new ToolBindingViolation(name, "expects a UUID string");
        }

        if (types.Contains("integer")
            && value.ValueKind == JsonValueKind.Number
            && !value.TryGetInt64(out _))
        {
            return new ToolBindingViolation(name, "expects an integer");
        }

        return null;
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

    private static string DescribeType(JsonElement schema)
    {
        var types = SchemaTypes(schema);
        var text = types.Count == 0 ? "any" : string.Join(" or ", types);
        if (schema.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String)
        {
            text = $"{text} ({format.GetString()})";
        }

        return text;
    }
}
