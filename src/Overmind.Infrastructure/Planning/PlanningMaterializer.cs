using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializer : IPlanningMaterializer
{
    private const string BacklogIndexRelativePath = "BACKLOG.md";

    private readonly OvermindDbContext _db;
    private readonly PlanningPolicy _policy;
    private readonly string _dataDirectory;

    public PlanningMaterializer(OvermindDbContext db, PlanningPolicy policy, string dataDirectory)
    {
        _db = db;
        _policy = policy;
        _dataDirectory = dataDirectory;
    }

    public async Task<PlanningMaterializationReport> SyncAsync(Guid projectId, bool force = false, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var project = await LoadProjectAsync(projectId, ct);
        var strategy = await _policy.ResolveMaterializationAsync(projectId, ct);
        if (strategy == PlanningMaterialization.Off)
        {
            return await ClearAsync(project, featureId: null, ct);
        }

        project.MarkMaterializationAttempt(startedAt);
        await _db.SaveChangesAsync(ct);

        var root = ResolveRoot(project, strategy);
        var rows = await _db.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var features = await _db.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.BlockedBy)
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        var entries = await _db.ContextEntries
            .Include(e => e.EntryTags)
            .ThenInclude(t => t.Tag)
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);

        var warnings = new List<string>();
        var skipped = new List<PlanningMaterializationSkip>();
        var refused = new List<PlanningMaterializationSkip>();
        var plans = BuildPlans(root, features, rows);
        plans.AddRange(BuildEntryPlans(root, entries));
        var otherRoot = OtherRoot(project, strategy);

        if (strategy == PlanningMaterialization.On)
        {
            GuardOverwrites(plans, rows, otherRoot, force, warnings, refused);
        }

        var (droppedRows, keptForeignRows) = await FindDroppedOtherRootRowsAsync(rows, strategy, otherRoot, ct);
        var droppedIds = droppedRows.Concat(keptForeignRows).Select(r => r.Id).ToHashSet();
        rows.RemoveAll(r => droppedIds.Contains(r.Id));

        // Captured before GuardLocalEdits prunes byte-stable plans: those rows stay tracked, not stale.
        var plannedPaths = plans.Select(p => p.RelativePath).ToHashSet(PathGuard.PathComparer);

        if (strategy == PlanningMaterialization.On)
        {
            await GuardLocalEditsAsync(plans, rows, force, warnings, skipped, refused, ct);
            await GuardStaleTrackedFilesAsync(root, rows, plannedPaths, force, refused, ct);
        }

        if (refused.Count > 0)
        {
            var failure = MaterializationException.Refused(refused);
            project.RecordMaterializationRun(MaterializationRunJson.Serialize(new MaterializationRunReport(
                startedAt, DateTime.UtcNow, Atomic: false, [], skipped.ToArray(), refused.ToArray())));
            project.MarkMaterializationFailed(failure.Message);
            await _db.SaveChangesAsync(ct);
            throw failure;
        }

        try
        {
            await ClearOtherRootAsync(
                project, droppedRows, keptForeignRows, strategy, otherRoot, warnings, rows, ct);

            await RemoveStaleTrackedFilesAsync(
                root, strategy == PlanningMaterialization.On, rows, plannedPaths, warnings, ct);

            await WritePlansAsync(projectId, plans, rows, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            project.RecordMaterializationRun(MaterializationRunJson.Serialize(new MaterializationRunReport(
                startedAt, DateTime.UtcNow, Atomic: false, [], skipped.ToArray(), [])));
            project.MarkMaterializationFailed(ex.Message);
            await _db.SaveChangesAsync(ct);
            throw;
        }

        var finishedAt = DateTime.UtcNow;
        var run = new MaterializationRunReport(
            startedAt,
            finishedAt,
            Atomic: true,
            plans.Select(p => p.RelativePath).ToArray(),
            skipped.ToArray(),
            []);
        project.RecordMaterializationRun(MaterializationRunJson.Serialize(run));
        project.MarkMaterialized(skipped);
        await _db.SaveChangesAsync(ct);

        return new PlanningMaterializationReport(
            projectId,
            strategy,
            plans.Select(p => new PlanningMaterializedFileInfo(p.RelativePath, p.Sha256)).ToArray(),
            warnings,
            skipped,
            Refused: [],
            Atomic: true,
            startedAt,
            finishedAt);
    }

    public async Task RecordSyncFailureAsync(Guid projectId, string error, CancellationToken ct = default)
    {
        // A failed sync can leave rolled-back row mutations staged in the change tracker; without
        // a clear they would ride along and commit with this save. The tracker is shared with the
        // calling service's scoped DbContext — callers must have saved their own state before the
        // sync guard runs, or this clear discards it.
        _db.ChangeTracker.Clear();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return;
        }

        project.MarkMaterializationFailed(error);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PlanningDriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await LoadProjectAsync(projectId, ct);
        var strategy = await _policy.ResolveMaterializationAsync(projectId, ct);
        if (strategy != PlanningMaterialization.On)
        {
            return [];
        }

        var rows = await _db.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var slugById = await _db.PlanningFeatures
            .Where(f => f.ProjectId == projectId)
            .ToDictionaryAsync(f => f.Id, f => f.Slug, ct);

        var items = new List<PlanningDriftItem>();
        foreach (var row in rows)
        {
            var absolute = PathGuard.ResolveUnderRoot(project.DiskPath, row.RelativePath);
            var featureSlug = row.FeatureId.HasValue && slugById.TryGetValue(row.FeatureId.Value, out var slug)
                ? slug
                : null;

            if (!File.Exists(absolute))
            {
                items.Add(new PlanningDriftItem(projectId, featureSlug, row.RelativePath, DriftKind.Missing));
                continue;
            }

            if (PathGuard.PathsEqual(row.RelativePath, BacklogIndexRelativePath))
            {
                var content = await File.ReadAllTextAsync(absolute, ct);
                if (!ManagedBlock.TryExtract(content, out var block) ||
                    !string.Equals(ContentHash.Sha256(block), row.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new PlanningDriftItem(projectId, featureSlug, row.RelativePath, DriftKind.Modified));
                }

                continue;
            }

            var bytes = await File.ReadAllBytesAsync(absolute, ct);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new PlanningDriftItem(projectId, featureSlug, row.RelativePath, DriftKind.Modified));
            }
        }

        return items;
    }

    public async Task<PlanningMaterializationReport> ClearAsync(Guid projectId, Guid? featureId = null, CancellationToken ct = default)
    {
        var project = await LoadProjectAsync(projectId, ct);
        return await ClearAsync(project, featureId, ct);
    }

    private async Task<PlanningMaterializationReport> ClearAsync(Project project, Guid? featureId, CancellationToken ct)
    {
        var strategy = await _policy.ResolveMaterializationAsync(project.Id, ct);

        var query = _db.PlanningMaterializedFiles.Where(m => m.ProjectId == project.Id);
        if (featureId.HasValue)
        {
            query = query.Where(m => m.FeatureId == featureId.Value);
        }

        var rows = await query.ToListAsync(ct);

        var warnings = new List<string>();
        var removed = new List<PlanningMaterializedFileInfo>();

        foreach (var row in rows)
        {
            await RemoveTrackedCopyAsync(project.DiskPath, rootIsRepo: true, row, warnings, ct);
            await RemoveTrackedCopyAsync(PlanningArchive.Root(_dataDirectory, project), rootIsRepo: false, row, warnings, ct);
            _db.PlanningMaterializedFiles.Remove(row);
            removed.Add(new PlanningMaterializedFileInfo(row.RelativePath, row.Sha256));
        }

        await _db.SaveChangesAsync(ct);

        return new PlanningMaterializationReport(project.Id, strategy, removed, warnings, []);
    }

    private async Task<(List<PlanningMaterializedFile> Dropped, List<PlanningMaterializedFile> KeptForeign)>
        FindDroppedOtherRootRowsAsync(
            List<PlanningMaterializedFile> rows, PlanningMaterialization strategy, string otherRoot, CancellationToken ct)
    {
        var dropped = new List<PlanningMaterializedFile>();
        var keptForeign = new List<PlanningMaterializedFile>();
        if (strategy is not (PlanningMaterialization.On or PlanningMaterialization.Archive))
        {
            return (dropped, keptForeign);
        }

        foreach (var row in rows)
        {
            switch (await InspectTrackedCopyAsync(otherRoot, row, ct))
            {
                case TrackedCopyRemoval.Removed:
                    dropped.Add(row);
                    break;
                case TrackedCopyRemoval.KeptForeign:
                    keptForeign.Add(row);
                    break;
            }
        }

        return (dropped, keptForeign);
    }

    private async Task ClearOtherRootAsync(
        Project project, List<PlanningMaterializedFile> droppedRows, List<PlanningMaterializedFile> keptForeignRows,
        PlanningMaterialization strategy, string otherRoot, List<string> warnings,
        List<PlanningMaterializedFile> rows, CancellationToken ct)
    {
        if ((droppedRows.Count == 0 && keptForeignRows.Count == 0) ||
            strategy is not (PlanningMaterialization.On or PlanningMaterialization.Archive))
        {
            return;
        }

        var otherIsRepo = strategy == PlanningMaterialization.Archive;

        foreach (var row in droppedRows.Concat(keptForeignRows))
        {
            var outcome = await RemoveTrackedCopyAsync(otherRoot, otherIsRepo, row, warnings, ct);
            if (outcome is TrackedCopyRemoval.NotFound or TrackedCopyRemoval.Failed)
            {
                // The pre-gate inspection pruned the row for the guards; a copy that refuses to
                // leave between the passes keeps its row so the write pass re-targets it instead
                // of colliding with the unique (ProjectId, RelativePath) index.
                rows.Add(row);
                continue;
            }

            _db.PlanningMaterializedFiles.Remove(row);
        }
    }

    // Read-only twin of the removal pass: same hash decisions, zero mutations, so the run can
    // still abort with an untouched tree once every guard and the drop set are known.
    private static async Task<TrackedCopyRemoval> InspectTrackedCopyAsync(
        string root, PlanningMaterializedFile row, CancellationToken ct)
    {
        var absolute = PathGuard.ResolveUnderRoot(root, row.RelativePath);
        if (!File.Exists(absolute) && !Directory.Exists(absolute))
        {
            return TrackedCopyRemoval.NotFound;
        }

        try
        {
            if (PathGuard.PathsEqual(row.RelativePath, BacklogIndexRelativePath))
            {
                var content = await File.ReadAllTextAsync(absolute, ct);
                if (!ManagedBlock.TryExtract(content, out var block))
                {
                    return TrackedCopyRemoval.NotFound;
                }

                return string.Equals(ContentHash.Sha256(block), row.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? TrackedCopyRemoval.Removed
                    : TrackedCopyRemoval.KeptForeign;
            }

            if (File.Exists(absolute))
            {
                var content = await File.ReadAllTextAsync(absolute, ct);
                return string.Equals(ContentHash.Sha256(content), row.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? TrackedCopyRemoval.Removed
                    : TrackedCopyRemoval.KeptForeign;
            }

            return TrackedCopyRemoval.Removed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return TrackedCopyRemoval.Failed;
        }
    }

    // Rows do not record which root they were written under, so a relative path alone proves
    // nothing about ownership: a file is only ours when its hash matches the tracked row.
    private static async Task<TrackedCopyRemoval> RemoveTrackedCopyAsync(
        string root, bool rootIsRepo, PlanningMaterializedFile row, List<string> warnings, CancellationToken ct)
    {
        var absolute = PathGuard.ResolveUnderRoot(root, row.RelativePath);
        if (!File.Exists(absolute) && !Directory.Exists(absolute))
        {
            return TrackedCopyRemoval.NotFound;
        }

        try
        {
            if (PathGuard.PathsEqual(row.RelativePath, BacklogIndexRelativePath))
            {
                var content = await File.ReadAllTextAsync(absolute, ct);
                if (!ManagedBlock.TryExtract(content, out var block))
                {
                    return TrackedCopyRemoval.NotFound;
                }

                if (!string.Equals(ContentHash.Sha256(block), row.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Kept foreign file at a tracked path: {absolute}");
                    return TrackedCopyRemoval.KeptForeign;
                }

                if (rootIsRepo)
                {
                    await File.WriteAllTextAsync(
                        absolute, ManagedBlock.Remove(content), ContentHash.Utf8NoBom, ct);
                }
                else
                {
                    File.Delete(absolute);
                    DirectoryPruner.PruneEmptyDirectories(absolute, root);
                }

                return TrackedCopyRemoval.Removed;
            }

            if (File.Exists(absolute))
            {
                var content = await File.ReadAllTextAsync(absolute, ct);
                if (!string.Equals(ContentHash.Sha256(content), row.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Kept foreign file at a tracked path: {absolute}");
                    return TrackedCopyRemoval.KeptForeign;
                }

                File.Delete(absolute);
                DirectoryPruner.PruneEmptyDirectories(absolute, root);
                return TrackedCopyRemoval.Removed;
            }

            // A directory squatting on the path: nothing to hash or delete, the row still goes.
            return TrackedCopyRemoval.Removed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not remove '{absolute}': {ex.Message}");
            return TrackedCopyRemoval.Failed;
        }
    }

    private static List<FilePlan> BuildPlans(
        string root, List<PlanningFeature> features, List<PlanningMaterializedFile> rows)
    {
        var slugById = features.ToDictionary(f => f.Id, f => f.Slug);
        var blockerOrders = BlockerOrdersByTaskId(features);
        var plans = new List<FilePlan>();

        foreach (var feature in features)
        {
            var (featurePart, slugPart) = BacklogNaming.SplitSlug(feature.Slug);
            var directory = $"BACKLOG/{feature.Slug}";

            plans.Add(FilePlan.Create(
                root, feature.Id, $"{directory}/feature.md",
                FeatureDocument(feature, featurePart, slugPart, slugById)));

            foreach (var artifact in feature.Artifacts.Where(a =>
                         a.Kind is PlanningArtifactKind.Spec or PlanningArtifactKind.Plan or PlanningArtifactKind.Result))
            {
                var name = artifact.Kind.ToString().ToLowerInvariant();
                plans.Add(FilePlan.Create(
                    root, feature.Id, $"{directory}/{name}.md",
                    ArtifactDocument(artifact, featurePart, slugPart, feature.Title)));
            }

            foreach (var task in feature.Artifacts
                         .Where(a => a.Kind == PlanningArtifactKind.Task)
                         .OrderBy(a => a.Order))
            {
                var relative = PinnedTaskPath(rows, feature.Id, directory, task.Order);
                if (relative is null)
                {
                    var title = FirstHeading(task.Body) ?? task.Brief;
                    var taskSlug = title is null
                        ? $"task-{task.Order:D2}"
                        : Slug.From(title, $"task-{task.Order:D2}");
                    relative = $"{directory}/tasks/{BacklogNaming.TaskFileName(task.Order, taskSlug)}";
                }

                blockerOrders.TryGetValue(task.Id, out var taskBlockers);
                plans.Add(FilePlan.Create(
                    root, feature.Id, relative,
                    TaskDocument(task, featurePart, slugPart, taskBlockers ?? [])));
            }
        }

        var duplicate = plans
            .GroupBy(p => p.AbsolutePath, PathGuard.PathComparer)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw MaterializationException.DuplicateTarget(duplicate.Key);
        }

        return plans;
    }

    private static string? PinnedTaskPath(
        List<PlanningMaterializedFile> rows, Guid featureId, string directory, int order)
    {
        var prefix = $"{directory}/tasks/{order:D2}-";
        var row = rows.FirstOrDefault(r =>
            r.FeatureId == featureId &&
            r.RelativePath.StartsWith(prefix, StringComparison.Ordinal) &&
            r.RelativePath.EndsWith(".md", StringComparison.Ordinal));

        return row?.RelativePath;
    }

    private static List<FilePlan> BuildEntryPlans(string root, List<ContextEntry> entries)
    {
        var plans = new List<FilePlan>(entries.Count);
        foreach (var entry in entries)
        {
            var id8 = entry.Id.ToString("N")[..8];
            var relative =
                $"ENTRIES/{entry.Type.ToString().ToLowerInvariant()}/{Slug.From(entry.Title, "entry")}-{id8}.md";
            plans.Add(FilePlan.Create(root, featureId: null, relative, EntryDocument(entry)));
        }

        var duplicate = plans
            .GroupBy(p => p.AbsolutePath, PathGuard.PathComparer)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw MaterializationException.DuplicateTarget(duplicate.Key);
        }

        return plans;
    }

    // Board values render verbatim, so line breaks inside a title or tag would split the
    // frontmatter line; they flatten here instead of a write-time refusal because rows stored
    // before any validator would still reach this projection.
    private static string FrontmatterLine(string value) =>
        value.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');

    private static string EntryDocument(ContextEntry entry)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"id: {entry.Id}\n");
        builder.Append($"type: {entry.Type.ToString().ToLowerInvariant()}\n");
        builder.Append($"title: {FrontmatterLine(entry.Title)}\n");
        var tags = entry.EntryTags
            .Select(t => t.Tag.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (tags.Count > 0)
        {
            builder.Append($"tags: {string.Join(", ", tags.Select(FrontmatterLine))}\n");
        }

        builder.Append($"createdAt: {entry.CreatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n");
        builder.Append($"updatedAt: {entry.UpdatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n");
        builder.Append("---\n\n");
        builder.Append(entry.Content);
        return builder.ToString();
    }

    private static void GuardOverwrites(
        List<FilePlan> plans, List<PlanningMaterializedFile> rows, string otherRoot, bool force,
        List<string> warnings, List<PlanningMaterializationSkip> refused)
    {
        foreach (var plan in plans)
        {
            if (!File.Exists(plan.AbsolutePath) && !Directory.Exists(plan.AbsolutePath))
            {
                continue;
            }

            // A row proves ownership only when the previous root no longer holds a copy; after a
            // strategy switch the new root's file with a matching row is foreign until re-written.
            var owned = rows.Any(r =>
                PathGuard.PathsEqual(r.RelativePath, plan.RelativePath) &&
                !File.Exists(PathGuard.ResolveUnderRoot(otherRoot, r.RelativePath)));
            if (owned)
            {
                continue;
            }

            if (force)
            {
                warnings.Add($"Overwrote untracked file: {plan.AbsolutePath}");
                continue;
            }

            refused.Add(new PlanningMaterializationSkip(
                plan.RelativePath, "file exists on disk but is not tracked by Overmind"));
        }
    }

    // Repo-root syncs only: archive copies live under app data and are rewritten freely. A write
    // loses hand edits only when three hashes diverge — the row proves the file is tracked, the
    // disk hash drifting from the row proves local edits since our last write, and the plan hash
    // differing from the disk proves the rewrite would actually drop them. A drifted file refuses
    // and the whole run aborts before any write; a file whose disk hash equals both the row and
    // the plan skips as a deliberate no-op instead of rewriting identical bytes.
    private static async Task GuardLocalEditsAsync(
        List<FilePlan> plans, List<PlanningMaterializedFile> rows, bool force,
        List<string> warnings, List<PlanningMaterializationSkip> skipped,
        List<PlanningMaterializationSkip> refused, CancellationToken ct)
    {
        for (var i = plans.Count - 1; i >= 0; i--)
        {
            var plan = plans[i];
            var row = rows.FirstOrDefault(r => PathGuard.PathsEqual(r.RelativePath, plan.RelativePath));
            if (row is null || !File.Exists(plan.AbsolutePath))
            {
                continue;
            }

            var diskHash = await HashTrackedDiskCopyAsync(plan, ct);
            if (diskHash is null)
            {
                continue;
            }

            if (string.Equals(diskHash, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(diskHash, plan.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    skipped.Add(new PlanningMaterializationSkip(
                        plan.RelativePath, "content already matches the board"));
                    plans.RemoveAt(i);
                }

                continue;
            }

            if (string.Equals(diskHash, plan.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (force)
            {
                warnings.Add($"Overwrote locally modified file: {plan.AbsolutePath}");
                continue;
            }

            refused.Add(new PlanningMaterializationSkip(
                plan.RelativePath, "tracked file has local edits that are not on the board"));
        }
    }

    // A tracked row whose path dropped out of the plan means its board artifact is gone: the
    // file leaves on the next pass, no tombstone. A drifted copy refuses the run exactly like
    // a planned one would; force passes with the file kept foreign instead of destroyed.
    private static async Task GuardStaleTrackedFilesAsync(
        string root, List<PlanningMaterializedFile> rows, ISet<string> plannedPaths, bool force,
        List<PlanningMaterializationSkip> refused, CancellationToken ct)
    {
        foreach (var row in StaleRows(rows, plannedPaths))
        {
            if (!force &&
                await InspectTrackedCopyAsync(root, row, ct) == TrackedCopyRemoval.KeptForeign)
            {
                refused.Add(new PlanningMaterializationSkip(
                    row.RelativePath, "tracked file has local edits that are not on the board"));
            }
        }
    }

    private async Task RemoveStaleTrackedFilesAsync(
        string root, bool rootIsRepo, List<PlanningMaterializedFile> rows,
        ISet<string> plannedPaths, List<string> warnings, CancellationToken ct)
    {
        foreach (var row in StaleRows(rows, plannedPaths))
        {
            var outcome = await RemoveTrackedCopyAsync(root, rootIsRepo, row, warnings, ct);
            if (outcome is TrackedCopyRemoval.NotFound or TrackedCopyRemoval.Removed)
            {
                rows.Remove(row);
                _db.PlanningMaterializedFiles.Remove(row);
            }
        }
    }

    private static List<PlanningMaterializedFile> StaleRows(
        List<PlanningMaterializedFile> rows, ISet<string> plannedPaths) =>
        rows.Where(r => !plannedPaths.Contains(r.RelativePath)).ToList();

    private static async Task<string?> HashTrackedDiskCopyAsync(FilePlan plan, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(plan.AbsolutePath, ct);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private async Task WritePlansAsync(
        Guid projectId, List<FilePlan> plans, List<PlanningMaterializedFile> rows, CancellationToken ct)
    {
        var written = new List<string>();
        var preExisting = new HashSet<string>(PathGuard.PathComparer);
        string? failedPath = null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var plan in plans)
            {
                failedPath = plan.AbsolutePath;
                if (File.Exists(plan.AbsolutePath) || Directory.Exists(plan.AbsolutePath))
                {
                    preExisting.Add(plan.AbsolutePath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(plan.AbsolutePath)!);
                await File.WriteAllTextAsync(
                    plan.AbsolutePath, ContentHash.Normalize(plan.Content), ContentHash.Utf8NoBom, ct);
                written.Add(plan.AbsolutePath);

                var existingRow = rows.FirstOrDefault(r => PathGuard.PathsEqual(r.RelativePath, plan.RelativePath));
                if (existingRow is not null)
                {
                    existingRow.Refresh(plan.Sha256);
                }
                else
                {
                    _db.PlanningMaterializedFiles.Add(PlanningMaterializedFile.Create(
                        projectId, plan.FeatureId, plan.RelativePath, plan.Sha256));
                }
            }

            failedPath = null;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not MaterializationException and not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            foreach (var entry in _db.ChangeTracker.Entries<PlanningMaterializedFile>()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified))
            {
                entry.State = EntityState.Detached;
            }

            foreach (var path in written)
            {
                if (!preExisting.Contains(path))
                {
                    DirectoryPruner.TryDelete(path);
                }
            }

            throw MaterializationException.IoFailure(failedPath ?? "database", ex);
        }
    }

    private string ResolveRoot(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive ? PlanningArchive.Root(_dataDirectory, project) : project.DiskPath;

    private string OtherRoot(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive ? project.DiskPath : PlanningArchive.Root(_dataDirectory, project);

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw MaterializationException.MissingProject(projectId);

    private static string? FirstHeading(string body)
    {
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..];
            }
        }

        return null;
    }

    private static string FeatureDocument(
        PlanningFeature feature, string featurePart, string slugPart, IReadOnlyDictionary<Guid, string> slugById)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {FeatureStatus(feature.Status)}\n");
        builder.Append($"priority: {PriorityText(feature.Priority)}\n");
        builder.Append($"version: {feature.Version}\n");
        builder.Append($"created: {feature.CreatedAt:yyyy-MM-dd}\n");

        var blockers = feature.BlockedBy
            .Where(link => slugById.ContainsKey(link.BlockedByFeatureId))
            .Select(link => slugById[link.BlockedByFeatureId])
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToList();
        if (blockers.Count > 0)
        {
            builder.Append($"{FrontmatterKeyRegistry.DependenciesKey}: {string.Join(", ", blockers)}\n");
        }

        builder.Append("---\n\n");
        builder.Append($"# Feature {featurePart} — {feature.Title}\n\n");
        builder.Append(BacklogMarkdown.StripLeadingHeadings(PlanningSections.Normalize(feature.Body)));
        return builder.ToString();
    }

    private static string ArtifactDocument(
        PlanningArtifact artifact, string featurePart, string slugPart, string featureTitle)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {artifact.Status.ToString().ToLowerInvariant()}\n");
        builder.Append($"created: {artifact.CreatedAt:yyyy-MM-dd}\n");
        if (artifact.FrozenAt.HasValue)
        {
            builder.Append($"validated: {artifact.FrozenAt:yyyy-MM-dd}\n");
        }

        AppendPreservedFrontmatter(builder, artifact.PreservedFrontmatter);
        builder.Append("---\n\n");
        builder.Append($"# {artifact.Kind} {featurePart} — {featureTitle}\n\n");
        builder.Append(BacklogMarkdown.StripLeadingHeadings(PlanningSections.Normalize(artifact.Body)));
        return builder.ToString();
    }

    private static Dictionary<Guid, IReadOnlyList<int>> BlockerOrdersByTaskId(List<PlanningFeature> features)
    {
        var orders = new Dictionary<Guid, IReadOnlyList<int>>();
        foreach (var feature in features)
        {
            var siblingOrders = feature.Artifacts
                .Where(a => a.Kind == PlanningArtifactKind.Task)
                .ToDictionary(a => a.Id, a => a.Order);

            foreach (var task in feature.Artifacts)
            {
                if (task.Kind != PlanningArtifactKind.Task)
                {
                    continue;
                }

                orders[task.Id] = task.BlockedBy
                    .Where(d => siblingOrders.ContainsKey(d.BlockingId))
                    .Select(d => siblingOrders[d.BlockingId])
                    .OrderBy(o => o)
                    .ToList();
            }
        }

        return orders;
    }

    private static string TaskDocument(
        PlanningArtifact task, string featurePart, string slugPart, IReadOnlyList<int> blockerOrders)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"task: {task.Order:D2}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {task.Status.ToString().ToLowerInvariant()}\n");
        builder.Append($"review: {task.Review.ToString().ToLowerInvariant()}\n");
        builder.Append($"created: {task.CreatedAt:yyyy-MM-dd}\n");
        if (blockerOrders.Count > 0)
        {
            builder.Append(
                $"{FrontmatterKeyRegistry.DependenciesKey}: {string.Join(", ", blockerOrders.Select(o => $"{o:D2}"))}\n");
        }

        if (task.Brief is not null)
        {
            builder.Append($"brief: {task.Brief}\n");
        }

        AppendPreservedFrontmatter(builder, task.PreservedFrontmatter);
        builder.Append("---\n\n");
        builder.Append(BacklogMarkdown.CollapseLeadingHeadings(PlanningSections.Normalize(task.Body)));
        return builder.ToString();
    }

    private static void AppendPreservedFrontmatter(StringBuilder builder, string? preservedJson)
    {
        var preserved = PreservedFrontmatterJson.Deserialize(preservedJson);
        if (preserved is null)
        {
            return;
        }

        foreach (var key in preserved.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            builder.Append($"{key}: {preserved[key]}\n");
        }
    }

    private static string FeatureStatus(PlanningFeatureStatus status) => status switch
    {
        PlanningFeatureStatus.Draft => "draft",
        PlanningFeatureStatus.Ready => "ready",
        PlanningFeatureStatus.InProgress => "inProgress",
        PlanningFeatureStatus.Review => "review",
        PlanningFeatureStatus.Done => "done",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string PriorityText(FeaturePriority priority) => priority switch
    {
        FeaturePriority.P0 => "p0",
        FeaturePriority.P1 => "p1",
        FeaturePriority.P2 => "p2",
        FeaturePriority.P3 => "p3",
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };

    private enum TrackedCopyRemoval
    {
        NotFound,
        Removed,
        KeptForeign,
        Failed
    }

    private sealed record FilePlan(
        Guid? FeatureId,
        string RelativePath,
        string AbsolutePath,
        string Content,
        string Sha256)
    {
        public static FilePlan Create(string root, Guid? featureId, string relativePath, string content) =>
            new(featureId, relativePath, PathGuard.ResolveUnderRoot(root, relativePath), content, ContentHash.Sha256(content));
    }
}
