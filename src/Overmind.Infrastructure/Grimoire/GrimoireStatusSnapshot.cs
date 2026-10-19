namespace Overmind.Infrastructure.Grimoire;

public sealed record GrimoireStatusSnapshot(
    GrimoireDetectionResult Detection,
    string BookName,
    string OverridePath,
    bool Wired,
    string InstallChannel);
