namespace Overmind.Infrastructure.Kits;

public sealed record LegacyKitMigrationReport(
    int KitsMarkedLegacy,
    int ProjectsMigrated,
    int KitActivations,
    int ProjectsFailed)
{
    public static LegacyKitMigrationReport Empty { get; } = new(0, 0, 0, 0);
}
