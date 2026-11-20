namespace Overmind.Core;

public sealed class GlobalMarkerDeployment
{
    private GlobalMarkerDeployment()
    {
    }

    public Guid Id { get; private set; }
    public Guid KitId { get; private set; }
    public AgentPlatform Platform { get; private set; }
    public string EntryFilePath { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;
    public DateTime DeployedAt { get; private set; }

    public static GlobalMarkerDeployment Create(
        Guid kitId,
        AgentPlatform platform,
        string entryFilePath,
        string contentSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);

        return new GlobalMarkerDeployment
        {
            Id = Guid.NewGuid(),
            KitId = kitId,
            Platform = platform,
            EntryFilePath = entryFilePath,
            ContentSha256 = contentSha256,
            DeployedAt = DateTime.UtcNow
        };
    }

    public void Refresh(string contentSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);

        ContentSha256 = contentSha256;
        DeployedAt = DateTime.UtcNow;
    }
}
