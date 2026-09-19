using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record BacklogFeatureImportResult(
    bool Created,
    int ArtifactsCreated,
    int ArtifactsUpdated,
    IReadOnlyList<string> Kept,
    int TrackedFiles,
    PlanningMaterialization Materialization);
