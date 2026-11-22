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

        if (lanes.Count == 0)
        {
            return await ProjectAsync(projectId, ct);
        }

        var existing = await _db.PlanningLanes
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        var byName = existing.ToDictionary(
            l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);

        foreach (var upsert in lanes)
        {
            if (!byName.TryGetValue(upsert.Name, out var current))
            {
                throw PlanningException.LaneBirthRestricted(upsert.Name.Trim());
            }

            current.Update(upsert.Name, upsert.Contour, upsert.SerialOrder, upsert.Priority,
                LiveSurfacesJson.Serialize(upsert.LiveSurfaces), upsert.BoundAlias);
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

    public async Task<IReadOnlyList<LaneRow>> UpsertLaneAsync(
        Guid projectId, LaneUpsert lane, LaneWriteMode mode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lane);
        if (mode == LaneWriteMode.Create)
        {
            ValidateRoster([lane]);
        }
        else
        {
            ValidateLaneCaps(lane);
        }

        await RequireProjectAsync(projectId, ct);

        var existing = await _db.PlanningLanes
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        var current = existing.FirstOrDefault(l => Matches(lane.Name, l.Name));

        if (mode == LaneWriteMode.Create)
        {
            if (current is not null)
            {
                throw PlanningException.LaneNameDuplicate(current.Name);
            }

            if (existing.Count >= MaxLanesPerProject)
            {
                throw PlanningException.LaneRosterTooLarge(existing.Count + 1, MaxLanesPerProject);
            }

            _db.PlanningLanes.Add(PlanningLane.Create(
                projectId, lane.Name, lane.Contour, lane.SerialOrder, lane.Priority,
                LiveSurfacesJson.Serialize(lane.LiveSurfaces), lane.BoundAlias));
        }
        else
        {
            var target = current ?? throw PlanningException.MissingLane(lane.Name.Trim());

            // The route carries the name as the locator only: identity stays on the stored spelling,
            // so a case-different PUT edits the lane instead of renaming it.
            target.Update(target.Name, lane.Contour, lane.SerialOrder, lane.Priority,
                LiveSurfacesJson.Serialize(lane.LiveSurfaces), lane.BoundAlias);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.LaneNameDuplicate(lane.Name.Trim());
        }

        return await ProjectAsync(projectId, ct);
    }

    public async Task<LaneRow> CreateLaneAsync(
        Guid projectId, string name, string? contour = null, int? priority = null,
        string? boundAlias = null, CancellationToken ct = default)
    {
        ValidateRoster([new LaneUpsert(name, contour, null, priority, BoundAlias: boundAlias)]);
        await RequireProjectAsync(projectId, ct);

        var existing = await _db.PlanningLanes
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        var stored = existing.FirstOrDefault(l => Matches(name, l.Name));

        if (stored is null)
        {
            _db.PlanningLanes.Add(Birth(
                projectId, name.Trim(), [.. existing.Select(l => l.Name)], contour, priority, boundAlias));
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PlanningServiceGuard.IsUniqueViolation(ex))
        {
            throw PlanningException.LaneNameDuplicate(name.Trim());
        }

        var roster = await ProjectAsync(projectId, ct);
        return roster.First(l => Matches(name, l.Name));
    }

    public async Task<IReadOnlyList<LaneRow>> DeleteLaneAsync(
        Guid projectId, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await RequireProjectAsync(projectId, ct);

        var existing = await _db.PlanningLanes
            .Where(l => l.ProjectId == projectId)
            .ToListAsync(ct);
        var lane = existing.FirstOrDefault(l => Matches(name, l.Name));

        if (lane is null)
        {
            return await ProjectAsync(projectId, ct);
        }

        var openFeatures = await _db.PlanningFeatures
            .AsNoTracking()
            .Where(f => f.ProjectId == projectId && f.Lane != null && f.Status != PlanningFeatureStatus.Done)
            .Select(f => new { f.Slug, f.Lane })
            .ToListAsync(ct);

        var holding = openFeatures
            .Where(f => Matches(lane.Name, f.Lane!))
            .Select(f => f.Slug)
            .ToList();
        if (holding.Count > 0)
        {
            throw PlanningException.LaneStillOccupied(lane.Name, holding);
        }

        _db.PlanningLanes.Remove(lane);
        await _db.SaveChangesAsync(ct);

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
        var sessions = await ClaimSessionFactsAsync(features, ct);

        var byLane = features
            .GroupBy(f => f.Lane!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return [.. lanes
            .OrderBy(l => l.Priority.HasValue ? 0 : 1)
            .ThenBy(l => l.Priority)
            .ThenBy(l => l.SerialOrder.HasValue ? 0 : 1)
            .ThenBy(l => l.SerialOrder)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(l => ToRow(l, byLane.GetValueOrDefault(l.Name) ?? [], liveness, sessions))];
    }

    private async Task<IReadOnlyDictionary<Guid, ClaimSessionFacts>> ClaimSessionFactsAsync(
        List<PlanningFeature> features, CancellationToken ct)
    {
        var sessionIds = features
            .Where(f => f.ClaimSessionId is not null)
            .Select(f => f.ClaimSessionId!.Value)
            .Distinct()
            .ToList();
        if (sessionIds.Count == 0)
        {
            return new Dictionary<Guid, ClaimSessionFacts>();
        }

        return await _db.AgentSessions
            .AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Role, s.LastSeenAt })
            .ToDictionaryAsync(s => s.Id, s => new ClaimSessionFacts(s.Role, s.LastSeenAt), ct);
    }

    private static LaneRow ToRow(
        PlanningLane lane,
        List<PlanningFeature> features,
        IReadOnlyDictionary<Guid, AgentSessionLiveness> liveness,
        IReadOnlyDictionary<Guid, ClaimSessionFacts> sessions)
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

        var holderSummary = summaries
            .Where(s => s.ClaimState == AgentSessionLiveness.Alive)
            .OrderByDescending(s => features.First(f => f.Slug == s.Slug).ClaimedAt)
            .FirstOrDefault();
        var holder = holderSummary is null
            ? null
            : HolderOf(holderSummary, features, sessions);

        return new LaneRow(
            lane.Name,
            lane.Contour,
            lane.SerialOrder,
            lane.Priority,
            lane.BoundAlias,
            LiveSurfacesJson.Deserialize(lane.LiveSurfaces),
            summaries.Count(s => s.Status != PlanningFeatureStatus.Done),
            holder,
            summaries);
    }

    private static LaneHolder HolderOf(
        LaneFeatureSummary summary,
        List<PlanningFeature> features,
        IReadOnlyDictionary<Guid, ClaimSessionFacts> sessions)
    {
        var feature = features.First(f => f.Slug == summary.Slug);
        var facts = sessions.GetValueOrDefault(feature.ClaimSessionId!.Value);
        return new LaneHolder(
            summary.ClaimedByAlias!,
            summary.ClaimedByVendor!,
            summary.ClaimState!.Value,
            facts?.Role ?? AgentSession.DefaultRole,
            facts?.LastSeenAt);
    }

    private sealed record ClaimSessionFacts(string Role, DateTime LastSeenAt);

    private static void ValidateRoster(IReadOnlyList<LaneUpsert> lanes)
    {
        if (lanes.Count > MaxLanesPerProject)
        {
            throw PlanningException.LaneRosterTooLarge(lanes.Count, MaxLanesPerProject);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lane in lanes)
        {
            ValidateLaneCaps(lane);

            var name = lane.Name.Trim();
            if (!PlanningLane.IsAddressableName(name))
            {
                throw PlanningException.InvalidLaneName(name);
            }

            if (!seen.Add(name))
            {
                throw PlanningException.LaneNameDuplicate(name);
            }
        }
    }

    private static void ValidateLaneCaps(LaneUpsert lane)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lane.Name);

        var name = lane.Name.Trim();
        if (name.Length > PlanningLane.MaxNameLength)
        {
            throw new ArgumentException(
                $"Lane name '{name}' exceeds the {PlanningLane.MaxNameLength} character cap.");
        }

        if (lane.Contour is { } contour &&
            contour.Trim().Length > SoftCap.Ceiling(PlanningLane.MaxContourLength))
        {
            throw new ArgumentException(
                $"Lane contour exceeds the {SoftCap.Ceiling(PlanningLane.MaxContourLength)} character hard ceiling " +
                $"(soft cap {PlanningLane.MaxContourLength}).");
        }

        if (lane.BoundAlias is { } boundAlias && boundAlias.Trim().Length > PlanningLane.MaxBoundAliasLength)
        {
            throw new ArgumentException(
                $"Lane boundAlias exceeds the {PlanningLane.MaxBoundAliasLength} character cap.");
        }

        if (lane.Priority is < 0 or > PlanningLane.MaxPriority)
        {
            throw new ArgumentException(
                $"Lane priority must be between 0 and {PlanningLane.MaxPriority}.");
        }

        if (lane.LiveSurfaces is not null)
        {
            LiveSurfacesJson.Serialize(lane.LiveSurfaces);
        }
    }

    internal static PlanningLane Birth(
        Guid projectId, string name, IReadOnlyList<string> roster,
        string? contour = null, int? priority = null, string? boundAlias = null)
    {
        if (!PlanningLane.IsAddressableName(name))
        {
            throw PlanningException.InvalidLaneName(name);
        }

        if (roster.Count >= MaxLanesPerProject)
        {
            throw PlanningException.LaneRosterTooLarge(roster.Count + 1, MaxLanesPerProject);
        }

        return PlanningLane.Create(projectId, name, contour, serialOrder: null, priority, boundAlias: boundAlias);
    }

    private static bool Matches(string left, string right) =>
        string.Equals(left.Trim(), right, StringComparison.OrdinalIgnoreCase);

    private async Task<Project> RequireProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw PlanningException.MissingProject(projectId);
}
