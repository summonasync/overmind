using System.Security.Cryptography;
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

    public async Task<PlanningMaterializationReport> SyncAsync(
        Guid projectId, bool force = false, bool quarantine = false, CancellationToken ct = default)
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
        var quarantined = new List<PlanningMaterializationSkip>();
        var quarantinedStalePaths = new HashSet<string>(PathGuard.PathComparer);
        var plans = MirrorPlanRenderer.BuildPlans(root, features, rows);
        plans.AddRange(MirrorPlanRenderer.BuildEntryPlans(root, entries, rows));
        var otherRoot = OtherRoot(project, strategy);

        if (strategy == PlanningMaterialization.On)
        {
            GuardOverwrites(
                plans, rows, otherRoot, force, warnings, refused, quarantined, quarantinedStalePaths, quarantine);
        }

        var (droppedRows, keptForeignRows) = await FindDroppedOtherRootRowsAsync(rows, strategy, otherRoot, ct);
        var droppedIds = droppedRows.Concat(keptForeignRows).Select(r => r.Id).ToHashSet();
        rows.RemoveAll(r => droppedIds.Contains(r.Id));

        // Captured before GuardLocalEdits prunes byte-stable plans: those rows stay tracked, not stale.
        var plannedPaths = plans.Select(p => p.RelativePath).ToHashSet(PathGuard.PathComparer);

        if (strategy == PlanningMaterialization.On)
        {
            await GuardLocalEditsAsync(plans, rows, force, warnings, skipped, refused, quarantined, quarantine, ct);
            await GuardStaleTrackedFilesAsync(
                root, rows, plannedPaths, force, refused, quarantined, quarantinedStalePaths, quarantine, ct);
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

        var gitHygiene = await MirrorGitHygiene.EnsureAsync(root, ct);

        try
        {
            await ClearOtherRootAsync(
                project, droppedRows, keptForeignRows, strategy, otherRoot, warnings, rows, ct);

            await RemoveStaleTrackedFilesAsync(
                root, strategy == PlanningMaterialization.On, rows, plannedPaths, warnings, quarantinedStalePaths, ct);

            var written = await WritePlansAsync(projectId, root, plans, rows, warnings, ct);

            var finishedAt = DateTime.UtcNow;
            var complete = written.Count == plans.Count;
            var run = new MaterializationRunReport(
                startedAt,
                finishedAt,
                Atomic: complete,
                written.Select(p => p.RelativePath).ToArray(),
                skipped.ToArray(),
                [],
                quarantined.ToArray());
            project.RecordMaterializationRun(MaterializationRunJson.Serialize(run));
            project.MarkMaterialized(skipped);
            await _db.SaveChangesAsync(ct);

            return new PlanningMaterializationReport(
                projectId,
                strategy,
                written.Select(p => new PlanningMaterializedFileInfo(p.RelativePath, p.Sha256)).ToArray(),
                warnings,
                skipped,
                Refused: [],
                Atomic: complete,
                startedAt,
                finishedAt,
                GitHygiene: gitHygiene,
                Quarantined: quarantined.ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            project.RecordMaterializationRun(MaterializationRunJson.Serialize(new MaterializationRunReport(
                startedAt, DateTime.UtcNow, Atomic: false, [], skipped.ToArray(), [], quarantined.ToArray())));
            project.MarkMaterializationFailed(ex.Message);
            await _db.SaveChangesAsync(ct);
            throw;
        }
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

    private static void GuardOverwrites(
        List<MirrorFilePlan> plans, List<PlanningMaterializedFile> rows, string otherRoot, bool force,
        List<string> warnings, List<PlanningMaterializationSkip> refused,
        List<PlanningMaterializationSkip> quarantined, ISet<string> quarantinedStalePaths, bool quarantine)
    {
        for (var i = plans.Count - 1; i >= 0; i--)
        {
            var plan = plans[i];
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

            var refusal = new PlanningMaterializationSkip(
                plan.RelativePath, "file exists on disk but is not tracked by Overmind");
            if (quarantine)
            {
                quarantined.Add(refusal);
                plans.RemoveAt(i);
                // A quarantined path must also survive the stale sweep: the row can outlive the
                // pruned plan (an unreadable other-root copy keeps its row) and removing the
                // file would betray the report that named it untouched.
                quarantinedStalePaths.Add(plan.RelativePath);
                continue;
            }

            refused.Add(refusal);
        }
    }

    // Repo-root syncs only: archive copies live under app data and are rewritten freely. A write
    // loses hand edits only when three hashes diverge — the row proves the file is tracked, the
    // disk hash drifting from the row proves local edits since our last write, and the plan hash
    // differing from the disk proves the rewrite would actually drop them. A drifted file refuses
    // and the whole run aborts before any write; a file whose disk hash equals both the row and
    // the plan skips as a deliberate no-op instead of rewriting identical bytes.
    private static async Task GuardLocalEditsAsync(
        List<MirrorFilePlan> plans, List<PlanningMaterializedFile> rows, bool force,
        List<string> warnings, List<PlanningMaterializationSkip> skipped,
        List<PlanningMaterializationSkip> refused, List<PlanningMaterializationSkip> quarantined,
        bool quarantine, CancellationToken ct)
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

            var refusal = new PlanningMaterializationSkip(
                plan.RelativePath, "tracked file has local edits that are not on the board");
            if (quarantine)
            {
                quarantined.Add(refusal);
                plans.RemoveAt(i);
                continue;
            }

            refused.Add(refusal);
        }
    }

    // A tracked row whose path dropped out of the plan means its board artifact is gone: the
    // file leaves on the next pass, no tombstone. A drifted copy refuses the run exactly like
    // a planned one would; force passes with the file kept foreign instead of destroyed.
    private static async Task GuardStaleTrackedFilesAsync(
        string root, List<PlanningMaterializedFile> rows, ISet<string> plannedPaths, bool force,
        List<PlanningMaterializationSkip> refused, List<PlanningMaterializationSkip> quarantined,
        ISet<string> quarantinedStalePaths, bool quarantine, CancellationToken ct)
    {
        foreach (var row in StaleRows(rows, plannedPaths))
        {
            if (force ||
                await InspectTrackedCopyAsync(root, row, ct) != TrackedCopyRemoval.KeptForeign)
            {
                continue;
            }

            var refusal = new PlanningMaterializationSkip(
                row.RelativePath, "tracked file has local edits that are not on the board");
            if (quarantine)
            {
                quarantined.Add(refusal);
                quarantinedStalePaths.Add(row.RelativePath);
                continue;
            }

            refused.Add(refusal);
        }
    }

    private async Task RemoveStaleTrackedFilesAsync(
        string root, bool rootIsRepo, List<PlanningMaterializedFile> rows,
        ISet<string> plannedPaths, List<string> warnings, ISet<string> keptStalePaths, CancellationToken ct)
    {
        foreach (var row in StaleRows(rows, plannedPaths))
        {
            if (keptStalePaths.Contains(row.RelativePath))
            {
                continue;
            }

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

    private static async Task<string?> HashTrackedDiskCopyAsync(MirrorFilePlan plan, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(plan.AbsolutePath, ct);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    // One unreadable or unwritable target must not kill the pass: a per-plan failure lands as a
    // warning, its row mutation is dropped so the tracked row keeps describing the disk, and the
    // remaining plans still commit. Only a transaction-level failure (the save itself) aborts.
    private async Task<List<MirrorFilePlan>> WritePlansAsync(
        Guid projectId, string root, List<MirrorFilePlan> plans, List<PlanningMaterializedFile> rows,
        List<string> warnings, CancellationToken ct)
    {
        var written = new List<MirrorFilePlan>();
        var preExisting = new HashSet<string>(PathGuard.PathComparer);
        string? failedPath = null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var plan in plans)
            {
                failedPath = plan.AbsolutePath;
                var existed = File.Exists(plan.AbsolutePath) || Directory.Exists(plan.AbsolutePath);
                if (existed)
                {
                    preExisting.Add(plan.AbsolutePath);
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(plan.AbsolutePath)!);
                    await File.WriteAllTextAsync(
                        plan.AbsolutePath, ContentHash.Normalize(plan.Content), ContentHash.Utf8NoBom, ct);
                    written.Add(plan);

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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    warnings.Add($"Skipped '{plan.RelativePath}': {ex.Message}");
                    DetachPlanRow(plan.RelativePath);
                    if (!existed && File.Exists(plan.AbsolutePath))
                    {
                        DirectoryPruner.TryDelete(plan.AbsolutePath);
                        DirectoryPruner.PruneEmptyDirectories(plan.AbsolutePath, root);
                    }
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

            foreach (var path in written.Select(p => p.AbsolutePath))
            {
                if (!preExisting.Contains(path))
                {
                    DirectoryPruner.TryDelete(path);
                }
            }

            throw MaterializationException.IoFailure(failedPath ?? "database", ex);
        }

        return written;
    }

    private void DetachPlanRow(string relativePath)
    {
        foreach (var entry in _db.ChangeTracker.Entries<PlanningMaterializedFile>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified &&
                                 PathGuard.PathsEqual(e.Entity.RelativePath, relativePath)))
        {
            entry.State = EntityState.Detached;
        }
    }

    private string ResolveRoot(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive ? PlanningArchive.Root(_dataDirectory, project) : project.DiskPath;

    private string OtherRoot(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive ? project.DiskPath : PlanningArchive.Root(_dataDirectory, project);

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken ct) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
        ?? throw MaterializationException.MissingProject(projectId);

    private enum TrackedCopyRemoval
    {
        NotFound,
        Removed,
        KeptForeign,
        Failed
    }
}
