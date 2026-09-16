namespace Overmind.Infrastructure.Kits;

public sealed class KitScanCandidate
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public bool NameCollision { get; init; }

    public Guid? NameCollisionKitId { get; init; }

    public Guid? ReImportMatchKitId { get; init; }

    public List<KitScanSkill> Skills { get; } = [];

    public List<KitScanGate> Gates { get; } = [];

    public List<KitScanGate> Guards { get; } = [];

    public List<KitScanIssue> Issues { get; } = [];
}
