using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record TaskDependencyEntry(int Order, PlanningArtifactStatus Status, bool Closed)
{
    public static TaskDependencyEntry FromArtifact(PlanningArtifact artifact) =>
        new(artifact.Order, artifact.Status, IsClosed(artifact.Status));

    public static bool IsClosed(PlanningArtifactStatus status) =>
        status is PlanningArtifactStatus.Done or PlanningArtifactStatus.Review;
}
