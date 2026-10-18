using System.Text.Json;
using ModelContextProtocol;
using Overmind.Mcp;

namespace Overmind.Mcp.Planning;

using static Overmind.Mcp.Planning.PlanningToolResponses;

public sealed class PlanningArtifactAliasResolver(OvermindApiClient api)
{
    private const string AliasArtifactIdConflict =
        "Pass alias or artifactId, not both — alias resolves to the artifact id against the feature roster.";

    private const string AliasRequiresProjectId =
        "alias requires projectId: the alias is resolved against that project's feature roster.";

    private const string ArtifactAddressRequired =
        "Provide artifactId, or alias together with projectId — nothing addresses the artifact.";

    private sealed record ArtifactAlias(string FeatureSlug, string Kind, int? Order);

    public async Task<Guid> AddressArtifactAsync(Guid? artifactId, Guid? projectId, string? alias, CancellationToken ct)
    {
        if (alias is null)
        {
            return artifactId ?? throw new McpException(ArtifactAddressRequired);
        }

        if (artifactId is not null)
        {
            throw new McpException(AliasArtifactIdConflict);
        }

        if (projectId is null)
        {
            throw new McpException(AliasRequiresProjectId);
        }

        var parsed = ParseArtifactAlias(alias);
        var roster = await Guarded(() => api.GetAsync(
            $"/api/agent/projects/{projectId}/planning/features/{Uri.EscapeDataString(parsed.FeatureSlug)}/artifacts",
            ct));
        return ResolveRosterMatch(roster, parsed, alias.Trim());
    }

    private static Guid ResolveRosterMatch(JsonElement roster, ArtifactAlias wanted, string alias)
    {
        var present = new List<string>();
        var matches = new List<(Guid Id, string Label)>();
        if (roster.ValueKind == JsonValueKind.Object &&
            roster.TryGetProperty("artifacts", out var artifacts) &&
            artifacts.ValueKind == JsonValueKind.Array)
        {
            foreach (var artifact in artifacts.EnumerateArray())
            {
                var kind = artifact.TryGetProperty("kind", out var kindNode) &&
                    kindNode.ValueKind == JsonValueKind.String
                    ? kindNode.GetString()
                    : null;
                var order = artifact.TryGetProperty("order", out var orderNode) &&
                    orderNode.ValueKind == JsonValueKind.Number
                    ? (int?)orderNode.GetInt32()
                    : null;

                if (kind is null)
                {
                    continue;
                }

                present.Add($"{kind}/{order ?? 0}");

                if (!kind.Equals(wanted.Kind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (wanted.Order is { } expected && order != expected)
                {
                    continue;
                }

                if (artifact.TryGetProperty("artifactId", out var id) &&
                    id.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(id.GetString(), out var artifactId))
                {
                    matches.Add((artifactId, $"{kind}@{order ?? 0}"));
                }
            }
        }

        if (matches.Count == 1)
        {
            return matches[0].Id;
        }

        if (matches.Count == 0)
        {
            throw new McpException(
                $"No artifact matches alias '{alias}'. Present kind/order pairs: " +
                $"{(present.Count == 0 ? "(none)" : string.Join(", ", present))}.");
        }

        throw new McpException(
            $"Alias '{alias}' is ambiguous: feature '{wanted.FeatureSlug}' has {matches.Count} {wanted.Kind} " +
            $"artifacts ({string.Join(", ", matches.Select(m => m.Label))}). S/P/R aliases address the single " +
            "document of the kind — duplicate documents are a roster bug; resolve by artifactId, " +
            "planning_list_artifacts lists the pairs.");
    }

    private static ArtifactAlias ParseArtifactAlias(string alias)
    {
        var trimmed = alias.Trim();
        var separator = trimmed.IndexOf('/');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            throw new McpException(InvalidAliasText(alias));
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
                _ => null
            };

            if (kind is not null)
            {
                return new ArtifactAlias(slug, kind, null);
            }
        }
        else if (suffix[0] is 't' or 'T' && TryParseTaskOrder(suffix[1..]) is { } order)
        {
            return new ArtifactAlias(slug, "task", order);
        }

        throw new McpException(InvalidAliasText(alias));
    }

    private static int? TryParseTaskOrder(string digits) =>
        digits.Length > 0 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, out var order)
            ? (int?)order
            : null;

    private static string InvalidAliasText(string alias) =>
        $"Invalid artifact alias '{alias}'. Expected '<slug>/T<order>' for a task by order, " +
        "or '<slug>/S', '<slug>/P', '<slug>/R' for the single spec, plan or result.";
}
