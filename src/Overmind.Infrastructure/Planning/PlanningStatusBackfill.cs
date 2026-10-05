using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningStatusBackfill(
    OvermindDbContext db,
    AppSettingsService settings,
    PlanningMaterializationDispatcher materialization,
    ILogger<PlanningStatusBackfill> logger)
{
    public const string VersionSettingKey = "StatusHonestyBackfillVersion";

    private const int CurrentVersion = 1;

    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            if (await AlreadyAppliedAsync(ct))
            {
                return;
            }

            var liars = await db.PlanningFeatures
                .Where(f => f.Status == PlanningFeatureStatus.InProgress)
                .Where(f => !f.Artifacts.Any(a =>
                    a.Kind == PlanningArtifactKind.Task
                    && (a.Status == PlanningArtifactStatus.InProgress
                        || a.Status == PlanningArtifactStatus.Review
                        || a.Status == PlanningArtifactStatus.Done)))
                .ToListAsync(ct);

            foreach (var feature in liars)
            {
                feature.SetStatus(
                    PlanningFeatureStatus.Ready,
                    "data-fix: inProgress without a taken task → ready",
                    PlanningLogSource.Auto);
            }

            if (liars.Count > 0)
            {
                await db.SaveChangesAsync(ct);

                foreach (var projectId in liars.Select(f => f.ProjectId).Distinct())
                {
                    await materialization.DispatchAsync(projectId, ct);
                }

                logger.LogInformation(
                    "Status honesty backfill moved {Count} inProgress feature(s) without a taken task to ready",
                    liars.Count);
            }

            await settings.SetAsync(VersionSettingKey, CurrentVersion.ToString(CultureInfo.InvariantCulture), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Status honesty backfill failed; continuing startup");
        }
    }

    private async Task<bool> AlreadyAppliedAsync(CancellationToken ct)
    {
        var stored = await settings.GetAsync(VersionSettingKey, ct);

        return int.TryParse(stored, CultureInfo.InvariantCulture, out var version) && version >= CurrentVersion;
    }
}
