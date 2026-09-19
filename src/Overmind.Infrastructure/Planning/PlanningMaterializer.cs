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
        var project = await LoadProjectAsync(projectId, ct);
        var strategy = await _policy.ResolveMaterializationAsync(projectId, ct);
        if (strategy == PlanningMaterialization.Off)
        {
            return await ClearAsync(project, featureId: null, ct);
        }

        var root = ResolveRoot(project, strategy);
        var rows = await _db.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var features = await _db.PlanningFeatures
            .Include(f => f.Artifacts)
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        var warnings = new List<string>();
        var plans = BuildPlans(root, features, rows);
        var otherRoot = OtherRoot(project, strategy);

        if (strategy == PlanningMaterialization.On)
        {
            GuardOverwrites(plans, rows, otherRoot, force, warnings);
        }

        var dropped = await ClearOtherRootAsync(project, rows, strategy, otherRoot, warnings, ct);
        rows.RemoveAll(r => dropped.Contains(r.Id));

        plans.Add(BuildBacklogPlan(root, features));

        await WritePlansAsync(projectId, plans, rows, ct);

        return new PlanningMaterializationReport(
            projectId,
            strategy,
            plans.Select(p => new PlanningMaterializedFileInfo(p.RelativePath, p.Sha256)).ToArray(),
            warnings);
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
            await RemoveTrackedCopyAsync(ArchiveRoot(project), rootIsRepo: false, row, warnings, ct);
            _db.PlanningMaterializedFiles.Remove(row);
            removed.Add(new PlanningMaterializedFileInfo(row.RelativePath, row.Sha256));
        }

        await _db.SaveChangesAsync(ct);

        return new PlanningMaterializationReport(project.Id, strategy, removed, warnings);
    }

    private async Task<HashSet<Guid>> ClearOtherRootAsync(
        Project project, List<PlanningMaterializedFile> rows, PlanningMaterialization strategy, string otherRoot,
        List<string> warnings, CancellationToken ct)
    {
        var dropped = new HashSet<Guid>();
        if (strategy is not (PlanningMaterialization.On or PlanningMaterialization.Archive))
        {
            return dropped;
        }

        var otherIsRepo = strategy == PlanningMaterialization.Archive;

        foreach (var row in rows)
        {
            var outcome = await RemoveTrackedCopyAsync(otherRoot, otherIsRepo, row, warnings, ct);
            if (outcome is TrackedCopyRemoval.NotFound or TrackedCopyRemoval.Failed)
            {
                continue;
            }

            dropped.Add(row.Id);
            _db.PlanningMaterializedFiles.Remove(row);
        }

        return dropped;
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
        var plans = new List<FilePlan>();

        foreach (var feature in features)
        {
            var (featurePart, slugPart) = BacklogNaming.SplitSlug(feature.Slug);
            var directory = $"BACKLOG/{feature.Slug}";

            plans.Add(FilePlan.Create(
                root, feature.Id, $"{directory}/feature.md", FeatureDocument(feature, featurePart, slugPart)));

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
                    var heading = FirstHeading(task.Body);
                    var taskSlug = heading is null
                        ? $"task-{task.Order:D2}"
                        : Slug.From(heading, $"task-{task.Order:D2}");
                    relative = $"{directory}/tasks/{BacklogNaming.TaskFileName(task.Order, taskSlug)}";
                }

                plans.Add(FilePlan.Create(root, feature.Id, relative, TaskDocument(task, featurePart, slugPart)));
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

    private FilePlan BuildBacklogPlan(string root, List<PlanningFeature> features)
    {
        var absolute = PathGuard.ResolveUnderRoot(root, BacklogIndexRelativePath);
        var existing = File.Exists(absolute) ? File.ReadAllText(absolute) : null;
        var block = ManagedBlock.Render(features);
        var content = ManagedBlock.Merge(existing, block);

        return FilePlan.Create(root, featureId: null, BacklogIndexRelativePath, content) with
        {
            Sha256 = ContentHash.Sha256(block),
        };
    }

    private static void GuardOverwrites(
        List<FilePlan> plans, List<PlanningMaterializedFile> rows, string otherRoot, bool force,
        List<string> warnings)
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

            if (!force)
            {
                throw MaterializationException.Overwrite(plan.AbsolutePath);
            }

            warnings.Add($"Overwrote untracked file: {plan.AbsolutePath}");
        }
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
        strategy == PlanningMaterialization.Archive ? ArchiveRoot(project) : project.DiskPath;

    private string OtherRoot(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive ? project.DiskPath : ArchiveRoot(project);

    private string ArchiveRoot(Project project) =>
        Path.Combine(_dataDirectory, "projects", Slug.From(project.Name, project.Id.ToString("N")), "backlog");

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

    private static string FeatureDocument(PlanningFeature feature, string featurePart, string slugPart)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {FeatureStatus(feature.Status)}\n");
        builder.Append($"version: {feature.Version}\n");
        builder.Append($"created: {feature.CreatedAt:yyyy-MM-dd}\n");
        builder.Append("---\n\n");
        builder.Append($"# Feature {featurePart} — {feature.Title}\n\n");
        builder.Append(BacklogMarkdown.StripLeadingHeadings(feature.Body));
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
        builder.Append(BacklogMarkdown.StripLeadingHeadings(artifact.Body));
        return builder.ToString();
    }

    private static string TaskDocument(PlanningArtifact task, string featurePart, string slugPart)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"task: {task.Order:D2}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {task.Status.ToString().ToLowerInvariant()}\n");
        builder.Append($"review: {task.Review.ToString().ToLowerInvariant()}\n");
        builder.Append($"created: {task.CreatedAt:yyyy-MM-dd}\n");
        AppendPreservedFrontmatter(builder, task.PreservedFrontmatter);
        builder.Append("---\n\n");
        builder.Append(BacklogMarkdown.CollapseLeadingHeadings(task.Body));
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
        PlanningFeatureStatus.Done => "done",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
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
