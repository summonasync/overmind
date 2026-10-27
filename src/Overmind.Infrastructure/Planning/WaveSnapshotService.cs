using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class WaveSnapshotService
{
    public const int DefaultEntryLimit = 20;
    public const int MaxEntryLimit = 100;

    public static readonly string[] DefaultCoordinationTags = ["wave-sync", "lane-marshalling"];

    private readonly OvermindDbContext _db;
    private readonly FeatureClaimService _claims;
    private readonly DirectiveService _directives;

    public WaveSnapshotService(OvermindDbContext db, FeatureClaimService claims, DirectiveService directives)
    {
        _db = db;
        _claims = claims;
        _directives = directives;
    }

    public async Task<WaveSnapshotData> SnapshotAsync(
        Guid projectId,
        IReadOnlyList<string>? coordinationTags = null,
        int? entryLimit = null,
        CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var features = await _db.PlanningFeatures
            .Include(f => f.Artifacts)
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);

        var queue = features
            .Where(f => f.Status != PlanningFeatureStatus.Done)
            .OrderBy(f => f.Priority)
            .ThenBy(f => f.QueuePosition)
            .ThenBy(f => f.Slug)
            .ToList();
        var inFlight = queue
            .Where(f => f.ClaimSessionId is not null ||
                        f.Status is PlanningFeatureStatus.InProgress or PlanningFeatureStatus.Review)
            .ToList();
        var liveness = await _claims.ClaimLivenessAsync(projectId, inFlight, ct);
        var wantedTags = Normalize(coordinationTags ?? DefaultCoordinationTags);
        var limit = Math.Min(entryLimit ?? DefaultEntryLimit, MaxEntryLimit);
        var entries = await CoordinationEntriesAsync(projectId, wantedTags, limit, ct);
        var directiveCounts = await _directives.OpenCountsAsync(projectId, ct);
        var laneBoundAliases = await _db.PlanningLanes
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId && l.BoundAlias != null)
            .ToDictionaryAsync(l => l.Name, l => l.BoundAlias, StringComparer.OrdinalIgnoreCase, ct);

        return new WaveSnapshotData(
            projectId, DateTime.UtcNow, wantedTags, queue, inFlight, liveness, entries, directiveCounts,
            laneBoundAliases);
    }

    private async Task<List<ContextEntry>> CoordinationEntriesAsync(
        Guid projectId, IReadOnlyList<string> tags, int limit, CancellationToken ct)
    {
        if (tags.Count == 0)
        {
            return [];
        }

        return await _db.ContextEntries
            .Include(e => e.EntryTags).ThenInclude(t => t.Tag)
            .AsSplitQuery()
            .Where(e => e.ProjectId == projectId && e.EntryTags.Any(t => t.Tag != null && tags.Contains(t.Tag.Name)))
            .OrderByDescending(e => e.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    private static List<string> Normalize(IReadOnlyList<string> tags) => [.. tags
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.Ordinal)];
}
