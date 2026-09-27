namespace Overmind.Core;

public sealed class PlanningTaskDependency
{
    private PlanningTaskDependency()
    {
    }

    public Guid BlockedById { get; private set; }
    public Guid BlockingId { get; private set; }

    public PlanningArtifact Task { get; private set; } = null!;
    public PlanningArtifact BlockedBy { get; private set; } = null!;

    internal static PlanningTaskDependency Create(Guid blockedById, Guid blockingId)
    {
        return new PlanningTaskDependency
        {
            BlockedById = blockedById,
            BlockingId = blockingId
        };
    }
}
