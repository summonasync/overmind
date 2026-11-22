using System.Text.Json;
using Overmind.Mcp.ToolBinding;

namespace Overmind.Mcp;

// 266/T3, survey row 5: a slug rebuilt from memory ("251-global-materialization" when the real one
// is the long derived slug) and a projectId copied out of an old note both answered with a bare
// "was not found" — the recovery was always a list call plus a resend. The adapter owns the
// vocabulary reads, so the adapter adds the candidate: nearest slug when exactly one is near,
// registered projects when the id itself is unknown. Ambiguity is never guessed.
// The Host stays untouched: the hint costs one extra loopback read, and only on the error path.
internal static class NearMissHints
{
    public const string MissingFeature = "MissingFeature";
    public const string MissingProject = "MissingProject";

    public static async Task<string> AppendAsync(
        OvermindApiClient api, string message, ProblemDetails problem, string operation, CancellationToken ct)
    {
        return problem.Title switch
        {
            MissingFeature => await AppendSlugAsync(api, message, operation, ct),
            MissingProject => await AppendProjectsAsync(api, message, ct),
            _ => message,
        };
    }

    private static async Task<string> AppendSlugAsync(
        OvermindApiClient api, string message, string operation, CancellationToken ct)
    {
        if (FeatureTarget(operation) is not (var projectId, var slug))
        {
            return message;
        }

        var vocabulary = await ReadVocabularyAsync(api, projectId, ct);
        if (vocabulary.Count == 0)
        {
            return message;
        }

        var near = NearestName.Suggest(slug, vocabulary, wholeSegment: false);
        return near is not null
            ? $"{message} Did you mean '{near}'?"
            : $"{message} planning_list_features shows the {vocabulary.Count} slugs this project actually has.";
    }

    private static async Task<string> AppendProjectsAsync(OvermindApiClient api, string message, CancellationToken ct)
    {
        try
        {
            var rows = await api.GetQuietAsync("/api/projects", ct);
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
            {
                return message;
            }

            var named = rows.EnumerateArray()
                .Select(row => (Name: Text(row, "name"), Id: Text(row, "id")))
                .Where(row => row.Name is not null)
                .Select(row => $"{row.Name} ({row.Id})")
                .ToArray();

            return named.Length == 0
                ? message
                : $"{message} Registered projects: {string.Join(", ", named)} — list_projects shows paths and descriptions.";
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            return message;
        }
    }

    private static async Task<IReadOnlyList<string>> ReadVocabularyAsync(
        OvermindApiClient api, string projectId, CancellationToken ct)
    {
        try
        {
            var board = await api.GetQuietAsync(
                $"/api/agent/projects/{projectId}/planning/features", ct);
            var rows = board.ValueKind == JsonValueKind.Array
                ? board.EnumerateArray()
                : board.TryGetProperty("features", out var nested) && nested.ValueKind == JsonValueKind.Array
                    ? nested.EnumerateArray()
                    : [];

            return [.. rows.Select(row => Text(row, "slug"))
                .Where(slug => slug is not null)
                .Select(slug => slug!)];
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiNotFoundException or ApiUnreachableException)
        {
            return [];
        }
    }

    // "POST /api/agent/projects/{pid}/planning/features/{slug}/status" — the operation label the
    // client already builds. Only the features-addressed shape carries a slug to correct.
    private static (string, string)? FeatureTarget(string operation)
    {
        var path = operation.Split(' ', 2).LastOrDefault() ?? operation;
        var query = path.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            path = path[..query];
        }

        var segments = path.Split('/');
        for (var i = 0; i + 4 < segments.Length; i++)
        {
            if (segments[i] != "projects" || segments[i + 2] != "planning" || segments[i + 3] != "features")
            {
                continue;
            }

            var projectId = Uri.UnescapeDataString(segments[i + 1]);
            var slug = Uri.UnescapeDataString(segments[i + 4]);
            return projectId.Length > 0 && slug.Length > 0 ? (projectId, slug) : null;
        }

        return null;
    }

    private static string? Text(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object
        && row.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
