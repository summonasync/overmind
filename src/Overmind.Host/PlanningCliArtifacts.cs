using System.Text.Json;

namespace Overmind.Host;

internal sealed record PlanningCliArtifactAlias(string FeatureSlug, string Kind, int? Order)
{
    public static bool TryParse(string alias, out PlanningCliArtifactAlias parsed)
    {
        parsed = null!;
        var trimmed = alias.Trim();
        var separator = trimmed.IndexOf('/');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var slug = trimmed[..separator];
        var suffix = trimmed[(separator + 1)..];

        if (suffix.Length == 1)
        {
            var kind = char.ToUpperInvariant(suffix[0]) switch
            {
                'S' => "spec",
                'P' => "plan",
                'R' => "result",
                _ => null,
            };

            if (kind is null)
            {
                return false;
            }

            parsed = new PlanningCliArtifactAlias(slug, kind, null);
            return true;
        }

        if (suffix[0] is 't' or 'T' && int.TryParse(suffix[1..], out var order) && order >= 0)
        {
            parsed = new PlanningCliArtifactAlias(slug, "task", order);
            return true;
        }

        return false;
    }
}

internal static class PlanningCliArtifacts
{
    public static async Task<Guid> ResolveAsync(
        PlanningCliClient client,
        Guid projectId,
        PlanningCliArtifactAlias alias,
        string aliasText,
        CancellationToken ct)
    {
        var roster = await client.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(alias.FeatureSlug)}/artifacts",
            query: null,
            ct);

        using var document = JsonDocument.Parse(string.IsNullOrEmpty(roster.Body) ? "{}" : roster.Body);
        var artifacts = PlanningCliJson.Items(document.RootElement, "artifacts");

        var present = new List<string>();
        var matches = new List<Guid>();
        foreach (var artifact in artifacts)
        {
            var kind = PlanningCliJson.Text(artifact, "kind");
            if (kind.Length == 0)
            {
                continue;
            }

            var order = PlanningCliJson.Number(artifact, "order");
            present.Add($"{kind}/{order}");

            if (!kind.Equals(alias.Kind, StringComparison.OrdinalIgnoreCase)
                || (alias.Order is { } expected && order != expected))
            {
                continue;
            }

            if (artifact.TryGetProperty("artifactId", out var id)
                && id.ValueKind == JsonValueKind.String
                && Guid.TryParse(id.GetString(), out var artifactId))
            {
                matches.Add(artifactId);
            }
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count == 0)
        {
            throw new PlanningCliApiException(
                404,
                $"No artifact matches alias '{aliasText}'. Present kind/order pairs: "
                + $"{(present.Count == 0 ? "(none)" : string.Join(", ", present))}.");
        }

        throw new PlanningCliApiException(
            409,
            $"Alias '{aliasText}' is ambiguous: feature '{alias.FeatureSlug}' has {matches.Count} {alias.Kind} "
            + $"artifacts. S/P/R aliases address the single document of the kind — resolve by artifact GUID.");
    }
}
