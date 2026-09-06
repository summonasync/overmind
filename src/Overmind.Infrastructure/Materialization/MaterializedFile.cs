using Overmind.Core;

namespace Overmind.Infrastructure.Materialization;

public sealed class MaterializedFile
{
    private MaterializedFile()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid KitId { get; private set; }
    public AgentPlatform Platform { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime MaterializedAt { get; private set; }

    public static MaterializedFile Create(
        Guid projectId,
        Guid kitId,
        AgentPlatform platform,
        string relativePath,
        string sha256)
    {
        return new MaterializedFile
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KitId = kitId,
            Platform = platform,
            RelativePath = relativePath,
            Sha256 = sha256,
            MaterializedAt = DateTime.UtcNow
        };
    }

    public void Refresh(string sha256)
    {
        Sha256 = sha256;
        MaterializedAt = DateTime.UtcNow;
    }
}
