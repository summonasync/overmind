using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Kits;

// R5 of the kit split: the bundle declares the kit names it dropped (legacy[]), and every project
// that carried one of them takes the optional kits the same bundle ships. Activation rows move
// only through KitActivationService, so the mandatory-global refusal holds here as well.
public sealed class LegacyKitMigrationService(
    OvermindDbContext db,
    KitActivationService activation,
    IMaterializer materializer,
    ILogger<LegacyKitMigrationService> logger)
{
    public async Task<LegacyKitMigrationReport> RunAsync(BundledManifest manifest, CancellationToken ct)
    {
        if (manifest.Legacy.Count == 0)
        {
            return LegacyKitMigrationReport.Empty;
        }

        var kitsByName = (await db.SkillKits.ToListAsync(ct))
            .GroupBy(k => k.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var pending = manifest.Legacy
            .Select(name => kitsByName.TryGetValue(name, out var kit) ? kit : null)
            .Where(kit => kit is not null && !kit.Legacy)
            .ToList();

        if (pending.Count == 0)
        {
            return LegacyKitMigrationReport.Empty;
        }

        var successors = ResolveSuccessors(manifest, kitsByName);
        if (successors.Count == 0)
        {
            logger.LogWarning(
                "Legacy kit migration stopped: no optional kit from bundle version {BundleVersion} reached the library",
                manifest.BundleVersion);
            return LegacyKitMigrationReport.Empty;
        }

        var marked = 0;
        var projects = 0;
        var activations = 0;
        var failed = 0;

        foreach (var legacyKit in pending)
        {
            var links = await db.ProjectKits.Where(pk => pk.KitId == legacyKit.Id).ToListAsync(ct);
            var projectFailures = 0;
            foreach (var link in links)
            {
                try
                {
                    var (landed, clean) = await MigrateProjectAsync(link, successors, ct);
                    activations += landed;
                    projects++;
                    if (!clean)
                    {
                        projectFailures++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    projectFailures++;
                    logger.LogError(ex,
                        "Legacy kit '{KitName}' could not be replaced in project {ProjectId}; the kit stays unmarked and the project is retried on next boot",
                        legacyKit.Name, link.ProjectId);
                }
            }

            if (projectFailures > 0)
            {
                failed += projectFailures;
                continue;
            }

            legacyKit.MarkLegacy();
            marked++;
            logger.LogInformation(
                "Legacy bundled kit '{KitName}' marked legacy: {Projects} project(s) migrated to {Kits} optional kit(s)",
                legacyKit.Name, links.Count, successors.Count);
        }

        if (marked > 0 || projects > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new LegacyKitMigrationReport(marked, projects, activations, failed);
    }

    private List<SkillKit> ResolveSuccessors(
        BundledManifest manifest,
        Dictionary<string, SkillKit> kitsByName)
    {
        var successors = new List<SkillKit>();
        foreach (var bundled in manifest.Kits)
        {
            if (bundled.Mandatory)
            {
                continue;
            }

            if (!kitsByName.TryGetValue(bundled.Name, out var kit) || kit.Legacy)
            {
                logger.LogWarning(
                    "Legacy kit migration: bundled kit '{KitName}' is not in the library, migrated projects will not get it",
                    bundled.Name);
                continue;
            }

            successors.Add(kit);
        }

        return successors;
    }

    private async Task<(int Activations, bool Clean)> MigrateProjectAsync(
        ProjectKit link, IReadOnlyList<SkillKit> successors, CancellationToken ct)
    {
        // The retired kit's files leave the disk while its activation row still reads active, and
        // the row flips only once every successor landed: a project whose successor refused to land
        // keeps an active link, so the next boot recognises it as unfinished instead of leaving it
        // stripped of every skill it had. Materialization also refuses to write a file another kit
        // owns, so the successors cannot be activated over the retired kit's content.
        await materializer.DematerializeAsync(link.ProjectId, link.KitId, ct);

        var activations = 0;
        var clean = true;
        foreach (var kit in successors)
        {
            try
            {
                await activation.ActivateAsync(link.ProjectId, kit.Id, force: false, ct);
                activations++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                clean = false;
                logger.LogError(ex,
                    "Optional kit '{KitName}' refused to land in project {ProjectId}; the retired kit stays active there until the next boot finishes the swap",
                    kit.Name, link.ProjectId);
            }
        }

        if (clean && link.IsActive)
        {
            await activation.DeactivateAsync(link.ProjectId, link.KitId, ct);
        }

        return (activations, clean);
    }
}
