using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

// One query's worth of the read door: the fields the response carries plus the project that owns the
// artifact through its feature — 246 compares that project against the caller's conversation scope
// without reading the object twice.
public sealed record PlanningArtifactReadRow(
    Guid Id,
    PlanningArtifactKind Kind,
    PlanningArtifactStatus Status,
    int Version,
    int Order,
    DateTime UpdatedAt,
    string? Brief,
    bool Independent,
    string Body,
    Guid FeatureId,
    Guid OwningProjectId);
