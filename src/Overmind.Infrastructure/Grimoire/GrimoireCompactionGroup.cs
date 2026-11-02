namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireCompactionGroup(
    IReadOnlyList<Guid> MemoryIds,
    double Similarity,
    IReadOnlyList<GrimoireMemoryRecord> Entries);
