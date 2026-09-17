using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record BacklogArtifactImport(
    PlanningArtifactKind Kind,
    int Order,
    PlanningArtifactStatus Status,
    PlanningReviewLevel Review,
    string Body,
    DateTime? FrozenAt,
    DateTime CreatedAt,
    string SourcePath,
    int? ReassignedFrom = null);
