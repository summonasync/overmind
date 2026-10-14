using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record FeatureNumberCollision(
    string CollidingSlug,
    PlanningFeatureStatus CollidingStatus,
    int SuggestedNumber);
