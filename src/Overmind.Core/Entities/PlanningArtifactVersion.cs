namespace Overmind.Core;

public sealed class PlanningArtifactVersion
{
    private PlanningArtifactVersion()
    {
    }

    public Guid Id { get; private set; }
    public Guid ArtifactId { get; private set; }
    public int Version { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    internal static PlanningArtifactVersion Create(Guid artifactId, int version, string body)
    {
        return new PlanningArtifactVersion
        {
            Id = Guid.NewGuid(),
            ArtifactId = artifactId,
            Version = version,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };
    }
}
