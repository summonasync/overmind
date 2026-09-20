using System.Collections.Frozen;

namespace Overmind.Core;

public static class PlanningFeatureTransitions
{
    private static readonly FrozenDictionary<PlanningFeatureStatus, PlanningFeatureStatus[]> Allowed =
        new Dictionary<PlanningFeatureStatus, PlanningFeatureStatus[]>
        {
            [PlanningFeatureStatus.Draft] = [PlanningFeatureStatus.Ready, PlanningFeatureStatus.InProgress],
            [PlanningFeatureStatus.Ready] = [PlanningFeatureStatus.InProgress, PlanningFeatureStatus.Draft],
            [PlanningFeatureStatus.InProgress] =
                [PlanningFeatureStatus.Review, PlanningFeatureStatus.Ready, PlanningFeatureStatus.Draft],
            [PlanningFeatureStatus.Review] = [PlanningFeatureStatus.Done, PlanningFeatureStatus.InProgress],
            [PlanningFeatureStatus.Done] = [],
        }.ToFrozenDictionary();

    public static bool IsAllowed(PlanningFeatureStatus current, PlanningFeatureStatus target) =>
        Allowed[current].Contains(target);

    public static IReadOnlyList<PlanningFeatureStatus> AllowedTargets(PlanningFeatureStatus current) =>
        Allowed[current].AsReadOnly();
}
