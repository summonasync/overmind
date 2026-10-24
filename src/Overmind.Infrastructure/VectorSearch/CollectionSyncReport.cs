namespace Overmind.Infrastructure.VectorSearch;

public sealed record CollectionSyncReport(bool Recreated, ulong? PreviousDimensions);
