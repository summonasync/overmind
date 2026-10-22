using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class TaskDependencyService
{
    private readonly OvermindDbContext _db;
    private readonly PlanningMaterializationDispatcher _materialization;

    public TaskDependencyService(
        OvermindDbContext db,
        PlanningMaterializationDispatcher materialization)
    {
        _db = db;
        _materialization = materialization;
    }

    public async Task<IReadOnlyList<TaskDependencyEntry>> ReplaceEdgesAsync(
        Guid taskArtifactId, IReadOnlyList<int> blockedByOrders, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskArtifactId, ct);
        var siblings = await LoadSiblingsAsync(task.FeatureId, ct);

        var changed = ValidateAndReplace(task, blockedByOrders, siblings);

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
            var projectId = await GetProjectIdAsync(task.FeatureId, ct);
            await _materialization.DispatchAsync(projectId, ct);
        }

        return View(task, siblings);
    }

    public async Task<bool> StageBlockedByAsync(
        PlanningArtifact task, IReadOnlyList<int> blockedByOrders, CancellationToken ct = default)
    {
        var siblings = await LoadSiblingsAsync(task.FeatureId, ct);
        return StageBlockedBy(task, blockedByOrders, siblings);
    }

    public bool StageBlockedBy(
        PlanningArtifact task,
        IReadOnlyList<int> blockedByOrders,
        IReadOnlyDictionary<Guid, PlanningArtifact> siblings) =>
        ValidateAndReplace(task, blockedByOrders, siblings);

    public async Task<IReadOnlyList<string>> RestoreFromImportAsync(
        PlanningFeature feature,
        IReadOnlyList<TaskDependencyImport> imports,
        CancellationToken ct = default)
    {
        if (imports.Count == 0)
        {
            return [];
        }

        var siblings = await LoadSiblingsAsync(feature.Id, ct);
        if (siblings.Count == 0)
        {
            return [];
        }

        var notes = new List<string>();
        foreach (var import in imports)
        {
            var task = siblings.GetValueOrDefault(import.Task.Id);
            if (task is null)
            {
                continue;
            }

            var existing = BlockedOrdersOf(task, siblings);
            var added = RestoreOrders(import, task, siblings, notes)
                .Except(existing)
                .ToList();
            if (added.Count == 0)
            {
                continue;
            }

            try
            {
                StageBlockedBy(task, [.. existing, .. added], siblings);
            }
            catch (PlanningException ex) when (ex.Code == PlanningFailureCode.TaskDependencyCycle)
            {
                notes.Add(
                    $"unlinked: {import.SourcePath} depends on task {OrdersText(added)} — " +
                    "dependency cycle among the feature's tasks");
                continue;
            }

            foreach (var order in added)
            {
                notes.Add(
                    $"linked: {import.SourcePath} depends on task {order:D2} (restored from imported file)");
            }
        }

        return notes;
    }

    public async Task<IReadOnlyList<TaskDependencyEntry>> UnclosedBlockersAsync(
        Guid taskArtifactId, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskArtifactId, ct);
        if (task.BlockedBy.Count == 0)
        {
            return [];
        }

        var blockerIds = task.BlockedBy.Select(d => d.BlockingId).ToList();
        var blockers = await _db.PlanningArtifacts
            .Where(a => blockerIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        return task.BlockedBy
            .Select(d => blockers.GetValueOrDefault(d.BlockingId))
            .Where(b => b is not null && !TaskDependencyEntry.IsClosed(b.Status))
            .Select(b => TaskDependencyEntry.FromArtifact(b!))
            .OrderBy(e => e.Order)
            .ToList();
    }

    public async Task<IReadOnlyList<TaskBlockedState>> BlockedOrdersAsync(
        Guid featureId, CancellationToken ct = default)
    {
        var featureExists = await _db.PlanningFeatures.AnyAsync(f => f.Id == featureId, ct);
        if (!featureExists)
        {
            throw PlanningException.MissingFeature(featureId.ToString());
        }

        var tasks = await LoadSiblingsAsync(featureId, ct);

        return tasks.Values
            .OrderBy(t => t.Order)
            .Select(t => new TaskBlockedState(
                t.Order,
                t.BlockedBy.Any(d => tasks.TryGetValue(d.BlockingId, out var blocker)
                    && !TaskDependencyEntry.IsClosed(blocker.Status)),
                BlockedOrdersOf(t, tasks)))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> UnclosedBlockerOrdersAsync(
        Guid featureId, CancellationToken ct = default)
    {
        var tasks = await LoadSiblingsAsync(featureId, ct);

        return tasks.Values.ToDictionary(
            t => t.Order,
            t => UnclosedBlockerOrdersOf(t, tasks));
    }

    private bool ValidateAndReplace(
        PlanningArtifact task, IReadOnlyList<int> blockedByOrders,
        IReadOnlyDictionary<Guid, PlanningArtifact> siblings)
    {
        var claimed = new HashSet<int>();
        foreach (var order in blockedByOrders)
        {
            if (!claimed.Add(order))
            {
                throw PlanningException.BlockedByDuplicateOrder(order);
            }

            if (order == task.Order)
            {
                throw PlanningException.BlockedBySelfReference(order);
            }
        }

        var edges = new List<Guid>(blockedByOrders.Count);
        foreach (var order in blockedByOrders)
        {
            var blocker = siblings.Values.FirstOrDefault(s => s.Order == order);
            if (blocker is null)
            {
                throw PlanningException.BlockedByOrderUnknown(
                    order, siblings.Values.Select(s => s.Order).OrderBy(o => o).ToList());
            }

            edges.Add(blocker.Id);
        }

        AssertNoCycle(task.Order, edges, siblings);

        var changed = !task.BlockedBy.Select(d => d.BlockingId).ToHashSet().SetEquals(edges);
        task.ReplaceBlockedEdges(edges);
        return changed;
    }

    private static void AssertNoCycle(
        int taskOrder, IReadOnlyList<Guid> edges,
        IReadOnlyDictionary<Guid, PlanningArtifact> siblings)
    {
        var graph = siblings.Values.ToDictionary(
            s => s.Order,
            s => (IReadOnlyList<int>)BlockedOrdersOf(s, siblings));
        graph[taskOrder] = [.. edges.Select(id => siblings[id].Order).OrderBy(o => o)];

        foreach (var direct in graph[taskOrder])
        {
            var chain = FindChainTo(direct, taskOrder, graph, []);
            if (chain is not null)
            {
                throw PlanningException.TaskDependencyCycle(taskOrder, [taskOrder, .. chain, taskOrder]);
            }
        }
    }

    private static List<int>? FindChainTo(
        int current, int target, Dictionary<int, IReadOnlyList<int>> graph, HashSet<int> visited)
    {
        if (current == target)
        {
            return [];
        }

        if (!visited.Add(current))
        {
            return null;
        }

        foreach (var next in graph.GetValueOrDefault(current, []))
        {
            var tail = FindChainTo(next, target, graph, visited);
            if (tail is not null)
            {
                return [current, .. tail];
            }
        }

        return null;
    }

    public static IReadOnlyList<int> BlockedOrdersOf(
        PlanningArtifact task, IReadOnlyDictionary<Guid, PlanningArtifact> siblings) =>
        task.BlockedBy
            .Where(d => siblings.ContainsKey(d.BlockingId))
            .Select(d => siblings[d.BlockingId].Order)
            .OrderBy(o => o)
            .ToList();

    private static IReadOnlyList<int> UnclosedBlockerOrdersOf(
        PlanningArtifact task, IReadOnlyDictionary<Guid, PlanningArtifact> siblings) =>
        task.BlockedBy
            .Where(d => siblings.TryGetValue(d.BlockingId, out var blocker)
                && !TaskDependencyEntry.IsClosed(blocker.Status))
            .Select(d => siblings[d.BlockingId].Order)
            .OrderBy(o => o)
            .ToList();

    private static List<int> RestoreOrders(
        TaskDependencyImport import,
        PlanningArtifact task,
        IReadOnlyDictionary<Guid, PlanningArtifact> siblings,
        List<string> notes)
    {
        var wanted = new List<int>();
        var seen = new HashSet<int>();
        var orders = siblings.Values.Select(s => s.Order).ToHashSet();

        foreach (var token in import.DependsOn)
        {
            if (!int.TryParse(token, out var order))
            {
                notes.Add($"unlinked: {import.SourcePath} depends on {token} — not a task order");
                continue;
            }

            if (order == task.Order)
            {
                notes.Add(
                    $"unlinked: {import.SourcePath} depends on task {order:D2} — a task cannot block itself");
                continue;
            }

            if (!orders.Contains(order))
            {
                notes.Add(
                    $"unlinked: {import.SourcePath} depends on task {order:D2} — task not on the feature");
                continue;
            }

            if (seen.Add(order))
            {
                wanted.Add(order);
            }
        }

        return wanted;
    }

    private static string OrdersText(IReadOnlyList<int> orders) =>
        string.Join(", ", orders.Select(o => $"{o:D2}"));

    private static IReadOnlyList<TaskDependencyEntry> View(
        PlanningArtifact task, IReadOnlyDictionary<Guid, PlanningArtifact> siblings) =>
        task.BlockedBy
            .Select(d => siblings.GetValueOrDefault(d.BlockingId))
            .Where(b => b is not null)
            .Select(b => TaskDependencyEntry.FromArtifact(b!))
            .OrderBy(e => e.Order)
            .ToList();

    private async Task<PlanningArtifact> LoadTaskAsync(Guid taskArtifactId, CancellationToken ct)
    {
        var task = await _db.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .FirstOrDefaultAsync(a => a.Id == taskArtifactId, ct);

        if (task is null)
        {
            throw PlanningException.MissingArtifact(taskArtifactId);
        }

        return task;
    }

    private async Task<Dictionary<Guid, PlanningArtifact>> LoadSiblingsAsync(
        Guid featureId, CancellationToken ct)
    {
        return await _db.PlanningArtifacts
            .Include(a => a.BlockedBy)
            .Where(a => a.FeatureId == featureId && a.Kind == PlanningArtifactKind.Task)
            .ToDictionaryAsync(a => a.Id, ct);
    }

    private async Task<Guid> GetProjectIdAsync(Guid featureId, CancellationToken ct)
    {
        return await _db.PlanningFeatures
            .Where(f => f.Id == featureId)
            .Select(f => f.ProjectId)
            .FirstAsync(ct);
    }
}
