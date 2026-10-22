using System.Text.Json;

namespace Overmind.Mcp.Planning;

internal static class UpsertArtifactReadback
{
    // Runs only after a wire timeout: the upsert's addressable identity is unknown to the
    // adapter, so the probe resolves the feature roster first and then trusts the log tail —
    // a committed upsert is the one whose newest log line carries this call's logNote.
    internal static async Task<MutationReadback?> ResolveAsync(
        OvermindApiClient api, Guid projectId, string slug, string kind, int? order,
        string logNote, CancellationToken ct)
    {
        var roster = await api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(slug)}/artifacts?kind={kind}", ct);

        var candidates = roster.GetProperty("artifacts").EnumerateArray()
            .Where(a => a.GetProperty("kind").GetString()?.Equals(kind, StringComparison.OrdinalIgnoreCase) == true)
            .Select(a => new Candidate(
                a.GetProperty("artifactId").GetString()!,
                a.GetProperty("order").GetInt32()))
            .ToList();

        candidates = order.HasValue
            ? candidates.Where(c => c.Order == order.Value).ToList()
            : [.. candidates.OrderByDescending(c => c.Order)];

        foreach (var candidate in candidates)
        {
            var path = ProbePath(candidate.Id);
            if (LogEndsWith(await api.GetAsync(path, ct), logNote.Trim()))
            {
                return new MutationReadback(path, logNote, Target(kind, slug, order));
            }
        }

        return candidates.Count > 0
            ? new MutationReadback(ProbePath(candidates[0].Id), logNote, Target(kind, slug, order))
            : null;
    }

    private static string ProbePath(string artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}?sections=log";

    private static bool LogEndsWith(JsonElement artifact, string marker)
    {
        var body = artifact.GetProperty("body").GetString() ?? string.Empty;
        return LogSectionReader.SectionContentText(body) is { } log &&
            log.TrimEnd().EndsWith(marker, StringComparison.Ordinal);
    }

    private static string Target(string kind, string slug, int? order) =>
        order.HasValue
            ? $"the {kind} at order {order} of '{slug}'"
            : $"a {kind} of '{slug}'";

    private sealed record Candidate(string Id, int Order);
}
