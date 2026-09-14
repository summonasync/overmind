using Overmind.Core;

namespace Overmind.Infrastructure.Kits;

public sealed class KitReimportEntryResult
{
    public required KitReimportOutcome Outcome { get; init; }

    public required EntryType EntryType { get; init; }

    public required string Title { get; init; }

    public string? RelativePath { get; init; }

    public Guid? EntryId { get; init; }

    public string? ImportedContentHash { get; init; }
}
