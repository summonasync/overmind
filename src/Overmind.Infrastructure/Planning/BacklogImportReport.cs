namespace Overmind.Infrastructure.Planning;

public sealed record BacklogImportReport(
    int FeaturesCreated,
    int FeaturesUpdated,
    int ArtifactsCreated,
    int ArtifactsUpdated,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<BacklogImportNewKey> NewKeys,
    IReadOnlyList<string> Preserved,
    IReadOnlyList<string> CreatedFallbacks,
    string? NextStep);
