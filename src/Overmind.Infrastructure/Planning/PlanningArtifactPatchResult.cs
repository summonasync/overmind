using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record PlanningArtifactPatchResult(
    PlanningArtifact Artifact,
    bool NoChange,
    int? TickedIndex,
    string? TickedText);
