using System.Text.Json;

namespace Overmind.Infrastructure.Planning;

internal static class PreservedFrontmatterJson
{
    public static string? Serialize(IReadOnlyDictionary<string, string>? map)
    {
        if (map is null)
        {
            return null;
        }

        var ordered = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in map)
        {
            ordered[key] = value;
        }

        return JsonSerializer.Serialize(ordered);
    }

    public static IReadOnlyDictionary<string, string>? Deserialize(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
