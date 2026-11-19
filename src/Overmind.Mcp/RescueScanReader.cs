using System.Text.Json;
using ModelContextProtocol;

namespace Overmind.Mcp;

public sealed record RescueScanSource(
    string? GeneratedAt,
    IReadOnlyList<RescueCandidateRow> Candidates,
    string? PolicyText,
    string? PolicyError)
{
    public const string OwnerSet = "owner-set";
    public const string NotSet = "not-set";
    public const string Unreadable = "unreadable";

    public string Autonomy => PolicyText is { Length: > 0 }
        ? OwnerSet
        : PolicyError is null ? NotSet : Unreadable;

    public string Guidance => Autonomy switch
    {
        OwnerSet =>
            "the stored directive is the owner's standing permission — push and merge what the scan flags " +
            "rescue-worthy, then report; the PR flow and the review invariant still apply",
        NotSet =>
            "no rescue directive on record: a blank policy is a deliberate clearing, not a missing default, " +
            "and the adapter never re-seeds it — report the facts and ask the owner before pushing or merging",
        _ =>
            "the stored policy could not be read — report the facts and ask the owner before pushing or merging",
    };
}

public sealed class RescueScanReader(OvermindApiClient api)
{
    public async Task<RescueScanSource> ReadAsync(Guid projectId, string? slug, CancellationToken ct)
    {
        var (generatedAt, rows) = await CandidatesAsync(projectId, ct);
        var wanted = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim();
        var filtered = wanted is null
            ? rows
            : [.. rows.Where(row => string.Equals(row.Slug, wanted, StringComparison.OrdinalIgnoreCase))];

        var (policy, policyError) = await PolicyAsync(projectId, ct);

        return new RescueScanSource(generatedAt, filtered, policy, policyError);
    }

    private async Task<(string? GeneratedAt, IReadOnlyList<RescueCandidateRow> Rows)> CandidatesAsync(
        Guid projectId,
        CancellationToken ct)
    {
        JsonElement payload;
        try
        {
            payload = await api.GetAsync($"/api/agent/projects/{projectId}/planning/rescue-candidates", ct);
        }
        catch (ApiNotFoundException)
        {
            throw new McpException(
                $"project {projectId} has no rescue-candidates door — it is not registered on this host.");
        }
        catch (Exception ex) when (ex is ApiStatusException or ApiUnreachableException)
        {
            throw new McpException($"the rescue-candidates door did not answer: {ex.Message}");
        }

        var generatedAt = Text(payload, "generatedAt");

        return payload.TryGetProperty("candidates", out var candidates)
               && candidates.ValueKind == JsonValueKind.Array
            ? (generatedAt, [.. candidates.EnumerateArray().Select(RescueCandidateRow.From)])
            : (generatedAt, []);
    }

    private async Task<(string? Text, string? Error)> PolicyAsync(Guid projectId, CancellationToken ct)
    {
        try
        {
            var settings = await api.GetAsync($"/api/projects/{projectId}/claim-settings", ct);

            return (Text(settings, "rescuePolicy"), null);
        }
        catch (Exception ex) when (ex is ApiStatusException
                                   or ApiNotFoundException
                                   or ApiUnreachableException
                                   or SemanticSearchUnavailableException)
        {
            return (null,
                $"claim-settings did not answer ({ex.Message}), so the stored rescue policy is unknown; " +
                "the scan still reports facts but grants no autonomy");
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
