using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record BacklogFeatureImport(
    string Slug,
    string Title,
    string Body,
    PlanningFeatureStatus Status,
    FeaturePriority Priority,
    int Version,
    DateTime CreatedAt,
    IReadOnlyList<BacklogArtifactImport> Artifacts,
    IReadOnlyList<string> SourcePaths);
