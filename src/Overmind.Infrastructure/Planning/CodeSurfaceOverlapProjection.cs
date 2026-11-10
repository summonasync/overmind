using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record FeatureOverlapRow(
    string WithFeatureSlug,
    IReadOnlyList<string> SharedGlobs,
    IReadOnlyList<FeatureOverlapMatch> Matches);

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

                Append(overlaps, inFlight[i].Id,
                    new FeatureOverlapRow(
                        inFlight[j].Slug, shared, Matches(inFlight[i].Surfaces, inFlight[j].Surfaces)));
                Append(overlaps, inFlight[j].Id,
                    new FeatureOverlapRow(
                        inFlight[i].Slug, shared, Matches(inFlight[j].Surfaces, inFlight[i].Surfaces)));
            }
        }

        return overlaps.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<FeatureOverlapRow>)[.. pair.Value.Take(MaxRowsPerFeature)]);
    }

    private static IReadOnlyList<FeatureOverlapMatch> Matches(
        IReadOnlyList<string> surfaces, IReadOnlyList<string> partnerSurfaces)
    {
        var matches = new List<FeatureOverlapMatch>();
        foreach (var pattern in CodeSurfaceOverlap.SharedGlobs(surfaces, partnerSurfaces, SharedGlobsCap))
        {
            var best = CodeSurfaceOverlap.BestPartnerPattern(pattern, partnerSurfaces);
            if (best is null)
            {
                continue;
            }

            matches.Add(new FeatureOverlapMatch(
                pattern,
                best,
                CodeSurfaceOverlap.IsExactMatch(pattern, best)
                    ? FeatureOverlapMatchKind.Exact
                    : FeatureOverlapMatchKind.SharedDirectory));
        }

        return matches;
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
