namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireAgentOutcome(
    string Agent,
    string Outcome,
    string? ConfigPath,
    string? Error);
