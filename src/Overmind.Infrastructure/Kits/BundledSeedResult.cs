namespace Overmind.Infrastructure.Kits;

public sealed record BundledSeedResult(
    BundledSeedOutcome Outcome,
    int BundleVersion,
    int KitsImported,
    int KitsUpdated,
    int KitsSkipped,
    int KitsFailed,
    int KitsMarkedLegacy)
{
    public static BundledSeedResult Nothing(BundledSeedOutcome outcome) => new(outcome, 0, 0, 0, 0, 0, 0);
}
