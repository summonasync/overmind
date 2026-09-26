using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningArtifactStatusResult(
    Guid ArtifactId,
    PlanningArtifactStatus Status,
    int Version,
    string LastLogLine,
    string[]? Warnings,
    string? FeatureStatusChanged);
