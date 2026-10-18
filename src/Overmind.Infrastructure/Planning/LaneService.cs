using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class LaneService
{
    public const int MaxLanesPerProject = 64;

    private readonly OvermindDbContext _db;
    private readonly FeatureClaimService _claims;

    public LaneService(OvermindDbContext db, FeatureClaimService claims)
    {
        _db = db;
        _claims = claims;
    }

    public async Task<IReadOnlyList<LaneRow>> SetLanesAsync(
        Guid projectId, IReadOnlyList<LaneUpsert> lanes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ValidateRoster(lanes);
        await RequireProjectAsync(projectId, ct);

        var existing = await _db.PlanningLanes
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        var byName = existing.ToDictionary(
            l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);

        var removed = existing
            .Where(l => !lanes.Any(u => Matches(u.Name, l.Name)))
            .ToList();

        foreach (var lane in removed)
        {
            _db.PlanningLanes.Remove(lane);
        }

        if (removed.Count > 0)
        {
            var orphans = await _db.PlanningFeatures
                .Where(f => f.ProjectId == projectId && f.Lane != null)
                .ToListAsync(ct);

            foreach (var feature in orphans.Where(f => removed.Any(l => Matches(l.Name, f.Lane!))))
            {
                feature.SetLane(null);
            }
        }

        foreach (var upsert in lanes)
        {
            if (byName.TryGetValue(upsert.Name, out var current))
            {
                current.Update(upsert.Name, upsert.Contour, upsert.SerialOrder);
            }
            else
            {
                _db.PlanningLanes.Add(PlanningLane.Create(projectId, upsert.Name, upsert.Contour, upsert.SerialOrder));
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.LaneNameDuplicate(lanes[0].Name.Trim());
        }

        return await ProjectAsync(projectId, ct);
    }

    public async Task<IReadOnlyList<LaneRow>> ListLanesAsync(Guid projectId, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        return await ProjectAsync(projectId, ct);
    }

    public async Task<IReadOnlyList<string>> RosterNamesAsync(Guid projectId, CancellationToken ct = default) =>
        [.. (await _db.PlanningLanes
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .Select(l => l.Name)
            .ToListAsync(ct))
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];

    private async Task<IReadOnlyList<LaneRow>> ProjectAsync(Guid projectId, CancellationToken ct)
    {
        var lanes = await _db.PlanningLanes
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        var features = await _db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.ProjectId == projectId && f.Lane != null)
            .ToListAsync(ct);
        var liveness = await _claims.ClaimLivenessAsync(projectId, features, ct);

        var byLane = features
            .GroupBy(f => f.Lane!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return [.. lanes
            .OrderBy(l => l.SerialOrder.HasValue ? 0 : 1)
            .ThenBy(l => l.SerialOrder)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(l => ToRow(l, byLane.GetValueOrDefault(l.Name) ?? [], liveness))];
    }

    private static LaneRow ToRow(
        PlanningLane lane,
        List<PlanningFeature> features,
        IReadOnlyDictionary<Guid, AgentSessionLiveness> liveness)
    {
        var summaries = features
            .OrderBy(f => f.Slug, StringComparer.Ordinal)
            .Select(f => new LaneFeatureSummary(
                f.Slug,
                f.Status,
                f.ClaimedByAlias,
                f.ClaimedByVendor,
                f.ClaimSessionId is null ? null : liveness.GetValueOrDefault(f.Id)))
            .ToList();

        var holder = summaries
            .Where(s => s.ClaimState == AgentSessionLiveness.Alive)
            .OrderByDescending(s => features.First(f => f.Slug == s.Slug).ClaimedAt)
            .Select(s => new LaneHolder(s.ClaimedByAlias!, s.ClaimedByVendor!, s.ClaimState!.Value))
            .FirstOrDefault();

        return new LaneRow(
            lane.Name,
            lane.Contour,
            lane.SerialOrder,
            summaries.Count(s => s.Status != PlanningFeatureStatus.Done),
            holder,
            summaries);
    }

    private static void ValidateRoster(IReadOnlyList<LaneUpsert> lanes)
    {
        if (lanes.Count > MaxLanesPerProject)
        {
            throw PlanningException.LaneRosterTooLarge(lanes.Count, MaxLanesPerProject);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lane in lanes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lane.Name);

            var name = lane.Name.Trim();
            if (name.Length > PlanningLane.MaxNameLength)
            {
                throw new ArgumentException(
                    $"Lane name '{name}' exceeds the {PlanningLane.MaxNameLength} character cap.");
            }

            if (lane.Contour is { } contour && contour.Trim().Length > PlanningLane.MaxContourLength)
            {
                throw new ArgumentException(
                    $"Lane contour exceeds the {PlanningLane.MaxContourLength} character cap.");
            }

            if (!seen.Add(name))
            {
                throw PlanningException.LaneNameDuplicate(name);
            }
        }
    }

    private static bool Matches(string left, string right) =>
        string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);
}
