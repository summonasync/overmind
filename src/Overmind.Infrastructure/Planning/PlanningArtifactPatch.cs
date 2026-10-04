namespace Overmind.Infrastructure.Planning;

public sealed record PlanningArtifactPatch(
    string? Section = null,
    string? Content = null,
    int? TickIndex = null,
    string? TickText = null,
    string? Brief = null,
    string? LogNote = null,
    bool Untick = false);
