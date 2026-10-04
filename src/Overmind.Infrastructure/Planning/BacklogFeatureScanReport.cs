namespace Overmind.Infrastructure.Planning;

public sealed record BacklogFeatureScanReport(
    string Root,
    IReadOnlyList<BacklogFeatureScanEntry> Entries);
