namespace Overmind.Core;

public sealed class ProjectKit
{
    private ProjectKit()
    {
    }

    public Guid ProjectId { get; private set; }
    public Guid KitId { get; private set; }
    public bool IsActive { get; private set; }

    public static ProjectKit Create(Guid projectId, Guid kitId)
    {
        return new ProjectKit
        {
            ProjectId = projectId,
            KitId = kitId,
            IsActive = true
        };
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
