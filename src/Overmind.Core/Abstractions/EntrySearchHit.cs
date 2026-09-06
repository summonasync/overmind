namespace Overmind.Core;

public sealed record EntrySearchHit(
    Guid EntryId,
    string Title,
    EntryType Type,
    string Snippet,
    double Rank);
