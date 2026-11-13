using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class HumanInboxService(OvermindDbContext db, FeatureClaimService claims)
{
    public async Task<IReadOnlyList<HumanInboxEntry>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        var features = await db.PlanningFeatures
            .AsNoTracking()
            .Include(f => f.Artifacts)
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);

        var pending = features
            .Where(f => CeremonyPhasePlanner.DeriveLabel(
                f.Status,
                ArtifactStatus(f, PlanningArtifactKind.Spec),
                ArtifactStatus(f, PlanningArtifactKind.Plan),
                TaskTotal(f),
                TaskDone(f),
                ArtifactStatus(f, PlanningArtifactKind.Result))
                == CeremonyPhasePlanner.AwaitingHumanLabel)
            .OrderBy(f => f.QueuePosition)
            .ThenBy(f => f.Slug, StringComparer.Ordinal)
            .ToList();

        if (pending.Count == 0)
        {
            return [];
        }

        var liveness = await claims.ClaimLivenessAsync(projectId, pending, ct);

        return [.. pending.Select(f => new HumanInboxEntry(
            f.Slug,
            f.Title,
            CeremonyPhasePlanner.AwaitingHumanLabel,
            f.QueuePosition,
            f.ClaimedByAlias,
            f.ClaimSessionId is null ? null : liveness.GetValueOrDefault(f.Id),
            PendingSince(f)))];
    }

    private static DateTime PendingSince(PlanningFeature feature) =>
        feature.Artifacts.First(a => a.Kind == PlanningArtifactKind.Result).FrozenAt!.Value;

    private static PlanningArtifactStatus? ArtifactStatus(PlanningFeature feature, PlanningArtifactKind kind) =>
        feature.Artifacts.FirstOrDefault(a => a.Kind == kind)?.Status;

    private static int TaskTotal(PlanningFeature feature) =>
        feature.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Task);

    private static int TaskDone(PlanningFeature feature) =>
        feature.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Task && a.Status == PlanningArtifactStatus.Done);
}
