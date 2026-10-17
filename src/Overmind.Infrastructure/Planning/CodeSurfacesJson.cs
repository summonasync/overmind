using System.Text.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public static class CodeSurfacesJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string? Serialize(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        var kept = patterns
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (kept.Length > PlanningFeature.MaxCodeSurfaces)
        {
            throw new ArgumentException(
                $"A feature declares at most {PlanningFeature.MaxCodeSurfaces} code surface patterns.");
        }

        foreach (var pattern in kept)
        {
            if (pattern.Length > PlanningFeature.MaxCodeSurfaceLength)
            {
                throw new ArgumentException(
                    $"Code surface pattern '{pattern}' exceeds the {PlanningFeature.MaxCodeSurfaceLength} character cap.");
            }
        }

        return kept.Length == 0 ? null : JsonSerializer.Serialize(kept, Options);
    }

    public static IReadOnlyList<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(json, Options);
            return parsed is null ? [] : [.. parsed.Where(p => p.Length > 0)];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
