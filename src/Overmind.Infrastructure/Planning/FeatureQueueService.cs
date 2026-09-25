using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class FeatureQueueService
{
    private readonly OvermindDbContext _db;

    public FeatureQueueService(OvermindDbContext db)
    {
        _db = db;
    }

    public async Task<PlanningFeature> MoveUpAsync(Guid projectId, string slug, CancellationToken ct = default) =>
        await MoveAsync(projectId, slug, (band, feature) =>
        {
            var index = band.IndexOf(feature);
            if (index > 0)
            {
                (band[index - 1], band[index]) = (band[index], band[index - 1]);
            }
        }, ct);

    public async Task<PlanningFeature> MoveDownAsync(Guid projectId, string slug, CancellationToken ct = default) =>
        await MoveAsync(projectId, slug, (band, feature) =>
        {
            var index = band.IndexOf(feature);
            if (index < band.Count - 1)
            {
                (band[index], band[index + 1]) = (band[index + 1], band[index]);
            }
        }, ct);

    public async Task<PlanningFeature> MoveFirstAsync(Guid projectId, string slug, CancellationToken ct = default) =>
        await MoveAsync(projectId, slug, (band, feature) =>
        {
            band.Remove(feature);
            band.Insert(0, feature);
        }, ct);

    public async Task<PlanningFeature> MoveLastAsync(Guid projectId, string slug, CancellationToken ct = default) =>
        await MoveAsync(projectId, slug, (band, feature) =>
        {
            band.Remove(feature);
            band.Add(feature);
        }, ct);

    public async Task<PlanningFeature> MoveToPositionAsync(
        Guid projectId, string slug, int position, CancellationToken ct = default) =>
        await MoveAsync(projectId, slug, (band, feature) =>
        {
            if (position < 1 || position > band.Count)
            {
                throw PlanningException.InvalidQueuePosition(feature.Slug, position, band.Count);
            }

            band.Remove(feature);
            band.Insert(position - 1, feature);
        }, ct);

    public async Task<IReadOnlyList<PlanningFeature>> ReorderBandAsync(
        Guid projectId, FeaturePriority priority, IReadOnlyList<string> orderedSlugs, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var duplicates = orderedSlugs
            .GroupBy(s => s, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw PlanningException.InvalidQueueReorder(priority, $"duplicate slugs: {string.Join(", ", duplicates)}");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var projectFeatures = await _db.PlanningFeatures
            .Where(f => f.ProjectId == projectId && orderedSlugs.Contains(f.Slug))
            .ToDictionaryAsync(f => f.Slug, StringComparer.Ordinal, ct);

        var band = await LoadBandAsync(projectId, priority, ct);
        var wanted = new List<PlanningFeature>(orderedSlugs.Count);
        var unknown = new List<string>();
        var wrongBand = new List<string>();
        var finished = new List<string>();

        foreach (var slug in orderedSlugs)
        {
            if (!projectFeatures.TryGetValue(slug, out var feature))
            {
                unknown.Add(slug);
                continue;
            }

            if (feature.Priority != priority)
            {
                wrongBand.Add(slug);
                continue;
            }

            if (feature.Status == PlanningFeatureStatus.Done)
            {
                finished.Add(slug);
                continue;
            }

            wanted.Add(feature);
        }

        if (unknown.Count > 0)
        {
            throw PlanningException.InvalidQueueReorder(
                priority, $"slugs not on this project's board: {string.Join(", ", unknown)}");
        }

        if (wrongBand.Count > 0)
        {
            throw PlanningException.InvalidQueueReorder(
                priority,
                string.Join("; ", wrongBand.Select(s =>
                    $"feature '{s}' carries priority '{projectFeatures[s].Priority.ToString().ToLowerInvariant()}'")));
        }

        if (finished.Count > 0)
        {
            throw PlanningException.InvalidQueueReorder(
                priority, $"done features cannot re-enter the queue: {string.Join(", ", finished)}");
        }

        var listed = wanted.Select(f => f.Slug).ToHashSet(StringComparer.Ordinal);
        var missing = band.Where(f => !listed.Contains(f.Slug)).Select(f => f.Slug).ToList();
        if (missing.Count > 0)
        {
            throw PlanningException.InvalidQueueReorder(priority, $"the list is missing: {string.Join(", ", missing)}");
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            wanted[i].SetQueuePosition(i + 1);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return wanted;
    }

    private async Task<PlanningFeature> MoveAsync(
        Guid projectId, string slug, Action<List<PlanningFeature>, PlanningFeature> move, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var feature = await _db.PlanningFeatures
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Slug == slug, ct);
        if (feature is null)
        {
            throw PlanningException.MissingFeature(slug);
        }

        if (feature.Status == PlanningFeatureStatus.Done)
        {
            throw PlanningException.FeatureNotInQueue(slug);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var band = await LoadBandAsync(projectId, feature.Priority, ct);
        move(band, feature);
        Renumber(band);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return feature;
    }

    private async Task<List<PlanningFeature>> LoadBandAsync(
        Guid projectId, FeaturePriority priority, CancellationToken ct) =>
        await _db.PlanningFeatures
            .Where(f => f.ProjectId == projectId
                        && f.Priority == priority
                        && f.Status != PlanningFeatureStatus.Done)
            .OrderBy(f => f.QueuePosition)
            .ThenBy(f => f.Slug)
            .ToListAsync(ct);

    private static void Renumber(List<PlanningFeature> band)
    {
        for (var i = 0; i < band.Count; i++)
        {
            band[i].SetQueuePosition(i + 1);
        }
    }
}
