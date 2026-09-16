using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

public sealed class BundledSeedService
{
    public const string VersionSettingKey = "BundledSeedVersion";

    internal const string ManifestFileName = "manifest.json";

    private readonly OvermindDbContext _db;
    private readonly KitImporter _importer;
    private readonly AppSettingsService _settings;
    private readonly ILogger<BundledSeedService> _logger;
    private readonly string _bundledDirectory;

    public BundledSeedService(
        OvermindDbContext db,
        KitImporter importer,
        AppSettingsService settings,
        ILogger<BundledSeedService> logger,
        string bundledDirectory)
    {
        _db = db;
        _importer = importer;
        _settings = settings;
        _logger = logger;
        _bundledDirectory = bundledDirectory;
    }

    public async Task<BundledSeedResult> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var manifestPath = Path.Combine(_bundledDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                _logger.LogInformation(
                    "No bundled kit manifest at {ManifestPath}; bundled seeding skipped", manifestPath);
                return BundledSeedResult.Nothing(BundledSeedOutcome.NoBundle);
            }

            if (!BundledManifest.TryLoad(manifestPath, out var manifest))
            {
                _logger.LogWarning(
                    "Bundled kit manifest at {ManifestPath} is not valid; bundled seeding skipped", manifestPath);
                return BundledSeedResult.Nothing(BundledSeedOutcome.InvalidManifest);
            }

            var stored = await _settings.GetAsync(VersionSettingKey, ct);
            if (stored is null)
            {
                if (await _db.SkillKits.AnyAsync(ct))
                {
                    _logger.LogInformation("Skill kit library is not empty; bundled seeding skipped");
                    return BundledSeedResult.Nothing(BundledSeedOutcome.LibraryInUse);
                }

                var counts = await ImportMissingKitsAsync(manifest.Kits, ct);
                await StampVersionAsync(manifest.BundleVersion, ct);
                _logger.LogInformation(
                    "Seeded bundled kits at version {BundleVersion}: {Imported} imported, {Failed} failed",
                    manifest.BundleVersion, counts.Imported, counts.Failed);
                return new BundledSeedResult(
                    BundledSeedOutcome.Seeded, manifest.BundleVersion, counts.Imported, 0, 0, counts.Failed);
            }

            if (!int.TryParse(stored, CultureInfo.InvariantCulture, out var seededVersion))
            {
                _logger.LogWarning(
                    "Stored bundled seed version '{StoredVersion}' is not a number; bundled sync skipped", stored);
                return BundledSeedResult.Nothing(BundledSeedOutcome.SyncFailed);
            }

            if (seededVersion >= manifest.BundleVersion)
            {
                return BundledSeedResult.Nothing(BundledSeedOutcome.UpToDate);
            }

            var upgraded = await UpgradeAsync(manifest, ct);
            await StampVersionAsync(manifest.BundleVersion, ct);
            _logger.LogInformation(
                "Upgraded bundled kits to version {BundleVersion}: {Imported} imported, {Updated} updated, {Skipped} skipped, {Failed} failed",
                manifest.BundleVersion, upgraded.Imported, upgraded.Updated, upgraded.Skipped, upgraded.Failed);
            return new BundledSeedResult(
                BundledSeedOutcome.Upgraded, manifest.BundleVersion,
                upgraded.Imported, upgraded.Updated, upgraded.Skipped, upgraded.Failed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bundled kit sync failed; continuing startup");
            return BundledSeedResult.Nothing(BundledSeedOutcome.SyncFailed);
        }
    }

    private async Task<UpgradeCounts> UpgradeAsync(BundledManifest manifest, CancellationToken ct)
    {
        var existingByName = (await _db.SkillKits.AsNoTracking().ToListAsync(ct))
            .GroupBy(k => k.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var selections = new List<KitImportSelection>();
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var kitName in manifest.Kits)
        {
            if (!existingByName.TryGetValue(kitName, out var kit))
            {
                selections.Add(new KitImportSelection(kitName, kitName));
                continue;
            }

            if (kit.SourceKind != KitSourceKind.Bundled)
            {
                _logger.LogInformation(
                    "Bundled kit '{KitName}' already exists as {SourceKind}; left untouched",
                    kitName, kit.SourceKind);
                skipped++;
                continue;
            }

            try
            {
                await RepointSourceAsync(kit, kitName, ct);
                var report = await _importer.ReimportAsync(kit.Id, bundled: true, ct);
                updated++;
                if (report.ConflictedCount > 0)
                {
                    _logger.LogWarning(
                        "Bundled kit '{KitName}' upgrade kept {Conflicts} locally edited entrie(s) unresolved",
                        kitName, report.ConflictedCount);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Bundled kit '{KitName}' failed to upgrade", kitName);
            }
        }

        var counts = await ImportMissingKitsAsync(selections.Select(s => s.Name).ToList(), ct);
        return new UpgradeCounts(counts.Imported, updated, skipped, counts.Failed + failed);
    }

    private async Task<KitCounts> ImportMissingKitsAsync(IReadOnlyList<string> kitNames, CancellationToken ct)
    {
        if (kitNames.Count == 0)
        {
            return new KitCounts(0, 0);
        }

        var selections = kitNames.Select(name => new KitImportSelection(name, name)).ToArray();
        var report = await _importer.CommitAsync(_bundledDirectory, selections, bundled: true, ct);

        foreach (var failure in report.Kits.Where(k => !k.Success))
        {
            _logger.LogWarning(
                "Bundled kit '{KitName}' failed to import: {Error}", failure.Name, failure.Error);
        }

        var imported = report.Kits.Count(k => k.Success);
        return new KitCounts(imported, report.Kits.Count - imported);
    }

    private async Task RepointSourceAsync(SkillKit kit, string kitName, CancellationToken ct)
    {
        var currentSource = Path.GetFullPath(Path.Combine(_bundledDirectory, kitName));
        if (SourcesEqual(kit.SourcePath, currentSource))
        {
            return;
        }

        var tracked = await _db.SkillKits.FirstAsync(k => k.Id == kit.Id, ct);
        tracked.MarkBundled(currentSource);
        await _db.SaveChangesAsync(ct);
    }

    private async Task StampVersionAsync(int bundleVersion, CancellationToken ct)
    {
        await _settings.SetAsync(
            VersionSettingKey, bundleVersion.ToString(CultureInfo.InvariantCulture), ct);
    }

    private static bool SourcesEqual(string? stored, string current) =>
        stored is not null
        && string.Equals(
            Path.GetFullPath(stored).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private readonly record struct KitCounts(int Imported, int Failed);

    private readonly record struct UpgradeCounts(int Imported, int Updated, int Skipped, int Failed);
}
