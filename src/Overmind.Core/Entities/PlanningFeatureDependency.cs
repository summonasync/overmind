namespace Overmind.Core;

public sealed class PlanningFeatureDependency
{
    private PlanningFeatureDependency()
    {
    }

    public Guid FeatureId { get; private set; }
    public Guid BlockedByFeatureId { get; private set; }

    public PlanningFeature Feature { get; private set; } = null!;
    public PlanningFeature BlockedBy { get; private set; } = null!;

    internal static PlanningFeatureDependency Create(Guid featureId, Guid blockedByFeatureId)
    {
        return new PlanningFeatureDependency
        {
            FeatureId = featureId,
            BlockedByFeatureId = blockedByFeatureId
        };
    }
}
