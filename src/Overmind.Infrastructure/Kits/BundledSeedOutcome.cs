namespace Overmind.Infrastructure.Kits;

public enum BundledSeedOutcome
{
    Seeded = 0,
    Upgraded = 1,
    UpToDate = 2,
    LibraryInUse = 3,
    NoBundle = 4,
    InvalidManifest = 5,
    SyncFailed = 6
}
