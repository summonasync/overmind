namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireAssistedInstallResult(
    bool Degraded,
    bool Success,
    GrimoireDetectionResult? Detection,
    IReadOnlyList<string> Instructions,
    string? Error);
