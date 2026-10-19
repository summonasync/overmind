namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireDisconnectResult(
    bool Success,
    bool Wired,
    string? Error);
