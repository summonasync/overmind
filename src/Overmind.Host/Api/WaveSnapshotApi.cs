using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Mapping;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public static class WaveSnapshotApi
{
    internal static async Task<IResult> Snapshot(
        Guid projectId,
        string? tags,
        int? entryLimit,
        WaveSnapshotService snapshots,
        CancellationToken ct)
    {
        if (entryLimit is < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(entryLimit)] = ["entryLimit must be a positive int; omit it for the default cap."]
            });
        }

        try
        {
            var snapshot = await snapshots.SnapshotAsync(projectId, ParseTags(tags), entryLimit, ct);
            return Results.Ok(PlanningMapper.ToWaveSnapshotResponse(snapshot));
        }
        catch (PlanningException ex)
        {
            return PlanningErrors.Map(ex);
        }
    }

    private static IReadOnlyList<string>? ParseTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
