namespace Overmind.Infrastructure.Planning;

public sealed record CeremonyFeature(
    Guid ProjectId,
    string ProjectName,
    string FeatureSlug,
    string Title,
    string[] Reasons);
