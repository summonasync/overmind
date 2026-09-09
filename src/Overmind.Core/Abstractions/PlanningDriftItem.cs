namespace Overmind.Core;

public sealed record PlanningDriftItem(
    Guid ProjectId,
    string? FeatureSlug,
    string RelativePath,
    DriftKind Kind);
