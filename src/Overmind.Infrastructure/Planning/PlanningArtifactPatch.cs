namespace Overmind.Infrastructure.Planning;

public sealed record PlanningArtifactPatch(
    string? Section = null,
    string? Content = null,
    int? TickIndex = null,
    string? TickText = null,
    string? LogNote = null);
