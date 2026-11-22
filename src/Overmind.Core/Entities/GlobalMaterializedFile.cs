namespace Overmind.Core;

public sealed class GlobalMaterializedFile
{
    private GlobalMaterializedFile()
    {
    }

    public Guid Id { get; private set; }
    public Guid KitId { get; private set; }
    public AgentPlatform Platform { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime MaterializedAt { get; private set; }

    public static GlobalMaterializedFile Create(
        Guid kitId,
        AgentPlatform platform,
        string relativePath,
        string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        return new GlobalMaterializedFile
        {
            Id = Guid.NewGuid(),
            KitId = kitId,
            Platform = platform,
            RelativePath = relativePath,
            Sha256 = sha256,
            MaterializedAt = DateTime.UtcNow
        };
    }

    public void Refresh(string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        Sha256 = sha256;
        MaterializedAt = DateTime.UtcNow;
    }
}
