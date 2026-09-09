namespace Overmind.Core;

public sealed class PlanningMaterializedFile
{
    private PlanningMaterializedFile()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? FeatureId { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime MaterializedAt { get; private set; }

    public static PlanningMaterializedFile Create(
        Guid projectId,
        Guid? featureId,
        string relativePath,
        string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        return new PlanningMaterializedFile
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FeatureId = featureId,
            RelativePath = relativePath.Trim(),
            Sha256 = sha256.Trim(),
            MaterializedAt = DateTime.UtcNow
        };
    }

    public void Refresh(string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        Sha256 = sha256.Trim();
        MaterializedAt = DateTime.UtcNow;
    }
}
