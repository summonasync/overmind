namespace Overmind.Infrastructure.Kits;

public sealed class KitReimportReport
{
    public required Guid KitId { get; init; }

    public required string ScannedPath { get; init; }

    public List<KitReimportEntryResult> Entries { get; } = [];

    public int AddedCount => Entries.Count(e => e.Outcome == KitReimportOutcome.Added);

    public int UpdatedCount => Entries.Count(e => e.Outcome == KitReimportOutcome.Updated);

    public int KeptLocalCount => Entries.Count(e => e.Outcome == KitReimportOutcome.KeptLocal);

    public int ConflictedCount => Entries.Count(e => e.Outcome == KitReimportOutcome.Conflicted);

    public int RemovedCount => Entries.Count(e => e.Outcome == KitReimportOutcome.Removed);
}
