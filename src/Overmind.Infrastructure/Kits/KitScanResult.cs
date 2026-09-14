namespace Overmind.Infrastructure.Kits;

public sealed class KitScanResult
{
    public required string ScannedPath { get; init; }

    public bool Truncated { get; init; }

    public List<KitScanCandidate> Candidates { get; } = [];
}
