namespace Overmind.Infrastructure.Planning;

public sealed record FeatureDependencies(
    string Slug,
    IReadOnlyList<FeatureDependencyEntry> BlockedBy,
    IReadOnlyList<FeatureDependencyEntry> Blocks);
