using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class RescueCandidateService(OvermindDbContext db)
{
    public async Task<IReadOnlyList<RescueCandidate>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw PlanningException.MissingProject(projectId);

        var claimed = await db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.ProjectId == projectId
                       && f.ClaimSessionId != null
                       && (f.Status == PlanningFeatureStatus.InProgress || f.Status == PlanningFeatureStatus.Review))
            .ToListAsync(ct);

        if (claimed.Count == 0)
        {
            return [];
        }

        var sessionIds = claimed.Select(f => f.ClaimSessionId!.Value).Distinct().ToList();
        var sessions = await db.AgentSessions
            .AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var featureIds = claimed.Select(f => f.Id).ToList();
        var openSpanByFeature = (await db.FeatureClaimSpans
                .AsNoTracking()
                .Where(s => featureIds.Contains(s.FeatureId) && s.ReleasedAt == null)
                .ToListAsync(ct))
            .OrderByDescending(s => s.ClaimedAt)
            .GroupBy(s => s.FeatureId)
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;
        var candidates = new List<RescueCandidate>();

        foreach (var feature in claimed)
        {
            var liveness = sessions.TryGetValue(feature.ClaimSessionId!.Value, out var session)
                ? session.Liveness(now, project.ClaimStaleThresholdMinutes)
                : AgentSessionLiveness.Ended;

            if (liveness is not (AgentSessionLiveness.Stale or AgentSessionLiveness.Ended))
            {
                continue;
            }

            openSpanByFeature.TryGetValue(feature.Id, out var span);
            candidates.Add(new RescueCandidate(
                feature.Slug,
                feature.Title,
                feature.Status,
                liveness,
                span?.Alias ?? feature.ClaimedByAlias ?? string.Empty,
                span?.Vendor ?? feature.ClaimedByVendor ?? string.Empty,
                span?.ClaimedAt ?? feature.ClaimedAt ?? feature.UpdatedAt,
                span?.ByHuman ?? feature.ClaimedByHuman,
                feature.PinnedBranch,
                feature.PinnedWorktreePath,
                feature.UpdatedAt));
        }

        return [.. candidates
            .OrderBy(c => c.ClaimedAt)
            .ThenBy(c => c.Slug, StringComparer.Ordinal)];
    }
}
