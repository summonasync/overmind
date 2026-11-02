using System.Globalization;
using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public static class AgentToolArgs
{
    public static Guid RequiredProjectId(JsonElement args) => RequiredGuid(args, "projectId");

    public static Guid RequiredGuid(JsonElement args, string name)
    {
        if (!Guid.TryParse(RequiredString(args, name), out var value))
        {
            throw new ArgumentException($"Argument '{name}' must be a GUID.");
        }

        return value;
    }

    public static Guid? OptionalGuid(JsonElement args, string name)
    {
        var raw = OptionalString(args, name);
        if (raw is null)
        {
            return null;
        }

        return Guid.TryParse(raw, out var value)
            ? value
            : throw new ArgumentException($"Argument '{name}' must be a GUID.");
    }

    public static string RequiredString(JsonElement args, string name)
    {
        var raw = OptionalString(args, name);
        return raw ?? throw new ArgumentException($"Argument '{name}' is required and cannot be blank.");
    }

    public static string? OptionalString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var raw = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new ArgumentException($"Argument '{name}' must be a string.");

        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    public static int? OptionalInt(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : throw new ArgumentException($"Argument '{name}' must be an integer.");
    }

    public static bool OptionalBool(JsonElement args, string name, bool fallback = false)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False
            ? value.GetBoolean()
            : throw new ArgumentException($"Argument '{name}' must be a boolean.");
    }

    public static JsonElement? OptionalRaw(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.Clone();
    }

    public static IReadOnlyList<int>? OptionalIntList(JsonElement args, string name)
    {
        if (OptionalRaw(args, name) is not { } raw)
        {
            return null;
        }

        if (raw.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Argument '{name}' must be an array of integers.");
        }

        return [.. raw.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var parsed)
            ? parsed
            : throw new ArgumentException($"Argument '{name}' must contain integers only."))];
    }

    public static string BuildQuery(string path, JsonElement args, params string[] names)
    {
        var pairs = new List<string>();
        foreach (var name in names)
        {
            if (!args.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var raw = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => throw new ArgumentException($"Argument '{name}' must be a string, boolean or number.")
            };

            if (!string.IsNullOrWhiteSpace(raw))
            {
                pairs.Add($"{name}={Uri.EscapeDataString(raw.Trim())}");
            }
        }

        return pairs.Count == 0 ? path : $"{path}?{string.Join("&", pairs)}";
    }
}
