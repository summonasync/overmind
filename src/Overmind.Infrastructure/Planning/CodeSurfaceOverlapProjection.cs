using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record FeatureOverlapRow(string WithFeatureSlug, IReadOnlyList<string> SharedGlobs);

public static class CodeSurfaceOverlapProjection
{
    public const int MaxRowsPerFeature = 5;
    public const int SharedGlobsCap = 5;

    public static IReadOnlyDictionary<Guid, IReadOnlyList<FeatureOverlapRow>> Compute(
        IReadOnlyList<CodeSurfaceFeatureRow> features)
    {
        ArgumentNullException.ThrowIfNull(features);

        var inFlight = features
            .Where(IsInFlight)
            .Select(f => (f.Id, f.Slug, Surfaces: CodeSurfacesJson.Deserialize(f.CodeSurfaces)))
            .Where(f => f.Surfaces.Count > 0)
            .OrderBy(f => f.Slug, StringComparer.Ordinal)
            .ToArray();

        var overlaps = new Dictionary<Guid, List<FeatureOverlapRow>>();
        for (var i = 0; i < inFlight.Length; i++)
        {
            for (var j = i + 1; j < inFlight.Length; j++)
            {
                var shared = CodeSurfaceOverlap.SharedGlobs(
                    inFlight[i].Surfaces, inFlight[j].Surfaces, SharedGlobsCap);
                if (shared.Count == 0)
                {
                    continue;
                }

                Append(overlaps, inFlight[i].Id, new FeatureOverlapRow(inFlight[j].Slug, shared));
                Append(overlaps, inFlight[j].Id, new FeatureOverlapRow(inFlight[i].Slug, shared));
            }
        }

        return overlaps.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<FeatureOverlapRow>)[.. pair.Value.Take(MaxRowsPerFeature)]);
    }

    private static bool IsInFlight(CodeSurfaceFeatureRow feature) =>
        feature.ClaimSessionId is not null ||
        feature.Status is PlanningFeatureStatus.InProgress or PlanningFeatureStatus.Review;

    private static void Append(Dictionary<Guid, List<FeatureOverlapRow>> overlaps, Guid id, FeatureOverlapRow row)
    {
        if (!overlaps.TryGetValue(id, out var rows))
        {
            rows = [];
            overlaps[id] = rows;
        }

        rows.Add(row);
    }
}
