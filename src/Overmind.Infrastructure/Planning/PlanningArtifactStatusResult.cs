namespace Overmind.Infrastructure.Planning;

public sealed record PlanningArtifactStatusResult(
    string[]? Warnings,
    string? FeatureStatusChanged);
