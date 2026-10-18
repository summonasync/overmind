using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningCurrentResult(
    string State,
    PlanningCurrentFeatureView Feature,
    PlanningArtifact? Actionable,
    string? Reason,
    string[]? Warnings = null,
    IReadOnlyDictionary<int, TaskBlockedState>? BlockedStates = null);
