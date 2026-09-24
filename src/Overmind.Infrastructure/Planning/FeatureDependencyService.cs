using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class FeatureDependencyService
{
    private readonly OvermindDbContext _db;
    private readonly IPlanningMaterializer _materializer;
    private readonly ILogger<FeatureDependencyService> _logger;

    public FeatureDependencyService(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        ILogger<FeatureDependencyService> logger)
    {
        _db = db;
        _materializer = materializer;
        _logger = logger;
    }

    public async Task<FeatureDependencies> AddDependencyAsync(
        Guid projectId, string slug, string blockedBySlug, CancellationToken ct = default)
    {
        var graph = await LoadProjectGraphAsync(projectId, tracked: true, ct);
        var feature = Resolve(graph, slug);
        var blocker = Resolve(graph, blockedBySlug);

        var chain = FindChainTo(blocker, feature.Id, graph, []);
        if (chain is not null)
        {
            throw PlanningException.DependencyCycle(slug, blockedBySlug, [feature.Slug, .. chain, feature.Slug]);
        }

        feature.AddBlockedBy(blocker);
        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
        return BuildView(feature, graph);
    }

    public async Task<FeatureDependencies> RemoveDependencyAsync(
        Guid projectId, string slug, string blockedBySlug, CancellationToken ct = default)
    {
        var graph = await LoadProjectGraphAsync(projectId, tracked: true, ct);
        var feature = Resolve(graph, slug);
        var blocker = Resolve(graph, blockedBySlug);

        feature.RemoveBlockedBy(blocker);
        await _db.SaveChangesAsync(ct);
        await PlanningServiceGuard.SyncMaterializationAsync(_materializer, _logger, projectId, ct);
        return BuildView(feature, graph);
    }

    public async Task<FeatureDependencies> ListDependenciesAsync(
        Guid projectId, string slug, CancellationToken ct = default)
    {
        var graph = await LoadProjectGraphAsync(projectId, tracked: false, ct);
        var feature = Resolve(graph, slug);
        return BuildView(feature, graph);
    }

    public async Task<IReadOnlyList<FeatureDependencyEntry>> OpenBlockersAsync(
        Guid projectId, string slug, CancellationToken ct = default)
    {
        var view = await ListDependenciesAsync(projectId, slug, ct);
        return view.BlockedBy.Where(entry => !entry.Closed).ToList();
    }

    private async Task<Dictionary<Guid, PlanningFeature>> LoadProjectGraphAsync(
        Guid projectId, bool tracked, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            throw PlanningException.MissingProject(projectId);
        }

        var query = _db.PlanningFeatures
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.ToDictionaryAsync(f => f.Id, ct);
    }

    private static PlanningFeature Resolve(
        IReadOnlyDictionary<Guid, PlanningFeature> graph, string slug) =>
        graph.Values.FirstOrDefault(f => f.Slug == slug)
            ?? throw PlanningException.MissingFeature(slug);

    private static List<string>? FindChainTo(
        PlanningFeature current,
        Guid targetId,
        IReadOnlyDictionary<Guid, PlanningFeature> graph,
        HashSet<Guid> visited)
    {
        if (current.Id == targetId)
        {
            return [];
        }

        if (!visited.Add(current.Id))
        {
            return null;
        }

        foreach (var link in current.BlockedBy)
        {
            if (!graph.TryGetValue(link.BlockedByFeatureId, out var next))
            {
                continue;
            }

            var tail = FindChainTo(next, targetId, graph, visited);
            if (tail is not null)
            {
                return [current.Slug, .. tail];
            }
        }

        return null;
    }

    private static FeatureDependencies BuildView(
        PlanningFeature feature, IReadOnlyDictionary<Guid, PlanningFeature> graph) =>
        new(
            feature.Slug,
            feature.BlockedBy
                .Select(d => FeatureDependencyEntry.FromFeature(graph[d.BlockedByFeatureId]))
                .OrderBy(e => e.Slug, StringComparer.Ordinal)
                .ToList(),
            feature.GetBlocks(graph.Values)
                .Select(FeatureDependencyEntry.FromFeature)
                .OrderBy(e => e.Slug, StringComparer.Ordinal)
                .ToList());
}
