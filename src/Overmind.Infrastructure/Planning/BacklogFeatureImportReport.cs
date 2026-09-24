using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record BacklogFeatureImportReport(
    string Slug,
    string Title,
    PlanningFeatureStatus Status,
    bool Created,
    int ArtifactsCreated,
    int ArtifactsUpdated,
    IReadOnlyList<string> Notes,
    int TrackedFiles,
    PlanningMaterialization Materialization,
    string? NextStep);
