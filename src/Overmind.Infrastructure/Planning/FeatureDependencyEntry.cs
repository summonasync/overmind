using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record FeatureDependencyEntry(string Slug, PlanningFeatureStatus Status, bool Closed)
{
    public static FeatureDependencyEntry FromFeature(PlanningFeature feature) =>
        new(feature.Slug, feature.Status, IsClosed(feature.Status));

    public static bool IsClosed(PlanningFeatureStatus status) =>
        status is PlanningFeatureStatus.Done or PlanningFeatureStatus.Review;
}
