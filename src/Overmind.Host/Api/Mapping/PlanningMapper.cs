using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api.Mapping;

public static class PlanningMapper
{
    public static PlanningFeatureBoardItem ToBoardItem(PlanningFeature feature, bool isBlocked)
    {
        var artifacts = feature.Artifacts;
        var spec = artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Spec);
        var plan = artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Plan);
        var tasks = artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).ToList();

        return new PlanningFeatureBoardItem(
            feature.Slug,
            feature.Title,
            feature.Status,
            feature.Priority,
            feature.Version,
            spec?.Status,
            plan?.Status,
            tasks.Count,
            tasks.Count(t => t.Status == PlanningArtifactStatus.Done),
            feature.UpdatedAt,
            feature.IsCurrent,
            isBlocked);
    }

    public static bool HasOpenBlocker(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> projectFeatures) =>
        feature.BlockedBy.Any(link =>
            projectFeatures.TryGetValue(link.BlockedByFeatureId, out var blocker) &&
            !FeatureDependencyEntry.IsClosed(blocker.Status));

    public static PlanningFeatureResponse ToFeatureResponse(
        PlanningFeature feature, FeatureDependencies dependencies)
    {
        return new PlanningFeatureResponse(
            feature.Slug,
            feature.Title,
            feature.Body,
            feature.Status,
            feature.Priority,
            feature.Version,
            feature.CreatedAt,
            feature.UpdatedAt,
            feature.Artifacts
                .OrderBy(a => a.Kind)
                .ThenBy(a => a.Order)
                .Select(ToArtifactResponse)
                .ToArray(),
            dependencies.BlockedBy.Any(entry => !entry.Closed),
            [.. dependencies.BlockedBy.Select(ToDependencyEntryResponse)],
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)]);
    }

    public static PlanningFeatureDependenciesResponse ToDependenciesResponse(FeatureDependencies dependencies)
    {
        return new PlanningFeatureDependenciesResponse(
            dependencies.Slug,
            [.. dependencies.BlockedBy.Select(ToDependencyEntryResponse)],
            [.. dependencies.Blocks.Select(ToDependencyEntryResponse)]);
    }

    private static PlanningFeatureDependencyEntryResponse ToDependencyEntryResponse(
        FeatureDependencyEntry entry) =>
        new(entry.Slug, entry.Status, entry.Closed);

    public static PlanningArtifactResponse ToArtifactResponse(PlanningArtifact artifact)
    {
        return new PlanningArtifactResponse(
            artifact.Id,
            artifact.Kind,
            artifact.Status,
            artifact.Review,
            artifact.Order,
            artifact.Body,
            artifact.Brief,
            artifact.FrozenAt,
            artifact.Version,
            artifact.CreatedAt,
            artifact.UpdatedAt,
            PlanningLog.Parse(artifact.Body)
                .Select(l => new PlanningLogLineResponse(
                    l.Date?.ToString("yyyy-MM-dd"),
                    l.Text))
                .ToArray());
    }

    public static PlanningArtifactVersionResponse ToVersionResponse(PlanningArtifactVersion version)
    {
        return new PlanningArtifactVersionResponse(version.Version, version.Body, version.CreatedAt);
    }
}
