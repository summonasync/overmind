using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningBatchOp(
    PlanningBatchOpName Op,
    Guid? ArtifactId = null,
    PlanningArtifactPatch? Patch = null,
    string? Note = null,
    PlanningArtifactStatus? Status = null,
    string? LogNote = null,
    IReadOnlyList<PlanningTaskUpsert>? Tasks = null,
    NotificationKind? Kind = null,
    string? Text = null,
    string? Tab = null,
    string? FeatureSlug = null,
    string? From = null);
