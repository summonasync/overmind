using System.Text.Json;
using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public static class LiveSurfacesJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string? Serialize(IReadOnlyList<LiveSurfaceEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return null;
        }

        var kept = new List<LiveSurfaceEntry>();
        foreach (var entry in entries)
        {
            var glob = entry.Glob?.Trim() ?? string.Empty;
            var owner = entry.Owner?.Trim() ?? string.Empty;

            if (glob.Length == 0 && owner.Length == 0)
            {
                continue;
            }

            if (glob.Length == 0)
            {
                throw new ArgumentException("A live surface requires a repo-relative glob.");
            }

            if (!RepoGlobRule.IsValid(glob))
            {
                throw new ArgumentException(
                    $"Live surface glob '{glob}' is not a valid repo-relative glob using only * and **.");
            }

            if (owner.Length == 0)
            {
                throw new ArgumentException($"Live surface '{glob}' requires a non-empty owner alias.");
            }

            if (owner.Length > PlanningLane.MaxLiveSurfaceOwnerLength)
            {
                throw new ArgumentException(
                    $"Live surface owner '{owner}' exceeds the {PlanningLane.MaxLiveSurfaceOwnerLength} character cap.");
            }

            var normalized = new LiveSurfaceEntry(glob, owner);
            if (kept.Any(k => k.Glob == normalized.Glob && k.Owner == normalized.Owner))
            {
                continue;
            }

            kept.Add(normalized);
        }

        if (kept.Count > PlanningLane.MaxLiveSurfaces)
        {
            throw new ArgumentException(
                $"A lane declares at most {PlanningLane.MaxLiveSurfaces} live surfaces.");
        }

        return kept.Count == 0 ? null : JsonSerializer.Serialize(kept, Options);
    }

    public static IReadOnlyList<LiveSurfaceEntry> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<LiveSurfaceEntry[]>(json, Options);
            return parsed is null
                ? []
                : [.. parsed.Where(p => p.Glob.Length > 0 && p.Owner.Length > 0)];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
