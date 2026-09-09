using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Mapping;

public static class PlanningMapper
{
    public static PlanningFeatureBoardItem ToBoardItem(PlanningFeature feature)
    {
        var artifacts = feature.Artifacts;
        var spec = artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Spec);
        var plan = artifacts.FirstOrDefault(a => a.Kind == PlanningArtifactKind.Plan);
        var tasks = artifacts.Where(a => a.Kind == PlanningArtifactKind.Task).ToList();

        return new PlanningFeatureBoardItem(
            feature.Slug,
            feature.Title,
            feature.Status,
            feature.Version,
            spec?.Status,
            plan?.Status,
            tasks.Count,
            tasks.Count(t => t.Status == PlanningArtifactStatus.Done),
            feature.UpdatedAt);
    }

    public static PlanningFeatureResponse ToFeatureResponse(PlanningFeature feature)
    {
        return new PlanningFeatureResponse(
            feature.Slug,
            feature.Title,
            feature.Body,
            feature.Status,
            feature.Version,
            feature.CreatedAt,
            feature.UpdatedAt,
            feature.Artifacts
                .OrderBy(a => a.Kind)
                .ThenBy(a => a.Order)
                .Select(ToArtifactResponse)
                .ToArray());
    }

    public static PlanningArtifactResponse ToArtifactResponse(PlanningArtifact artifact)
    {
        return new PlanningArtifactResponse(
            artifact.Id,
            artifact.Kind,
            artifact.Status,
            artifact.Review,
            artifact.Order,
            artifact.Body,
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
