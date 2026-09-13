using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

internal static class PlanningServiceGuard
{
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;

    public static async Task SyncMaterializationAsync(
        IPlanningMaterializer materializer, ILogger logger, Guid projectId, CancellationToken ct)
    {
        try
        {
            await materializer.SyncAsync(projectId, force: false, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Planning materialization sync failed for project {ProjectId}", projectId);
        }
    }

    public static async Task ClearMaterializationAsync(
        IPlanningMaterializer materializer, ILogger logger, Guid projectId, Guid featureId, CancellationToken ct)
    {
        try
        {
            await materializer.ClearAsync(projectId, featureId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Planning materialization clear failed for feature {FeatureId} in project {ProjectId}",
                featureId, projectId);
        }
    }
}
