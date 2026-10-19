namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireDetectionResult(
    GrimoireStatus Status,
    string? BinaryPath,
    string? Version,
    int? Port);
