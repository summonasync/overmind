using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record CodeSurfaceFeatureRow(
    Guid Id,
    string Slug,
    PlanningFeatureStatus Status,
    Guid? ClaimSessionId,
    string? CodeSurfaces);
