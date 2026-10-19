namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireWireResult(
    bool Success,
    bool Wired,
    IReadOnlyList<GrimoireAgentOutcome> Agents,
    string? Error);
