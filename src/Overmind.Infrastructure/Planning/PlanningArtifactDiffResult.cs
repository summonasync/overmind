namespace Overmind.Infrastructure.Planning;

public sealed record PlanningArtifactDiffResult(
    Guid ArtifactId,
    int FromVersion,
    int ToVersion,
    string Diff);
