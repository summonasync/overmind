namespace Overmind.Infrastructure.Planning;

public sealed record BacklogImportReport(
    int FeaturesCreated,
    int FeaturesUpdated,
    int ArtifactsCreated,
    int ArtifactsUpdated,
    IReadOnlyList<string> Skipped);
