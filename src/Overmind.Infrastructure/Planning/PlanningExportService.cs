using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningExportService(OvermindDbContext db)
{
    public async Task<PlanningExportResult> ExportAsync(
        Guid projectId, string targetPath, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw PlanningException.MissingProject(projectId);

        var trimmed = targetPath.Trim();
        if (!Path.IsPathFullyQualified(trimmed))
        {
            throw PlanningException.ExportTargetNotAbsolute(trimmed);
        }

        var target = Path.GetFullPath(trimmed);
        RefuseMirrorCollision(project.DiskPath, target);
        RefuseNonEmptyTarget(target);

        var rows = await db.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var features = await db.PlanningFeatures
            .Include(f => f.Artifacts)
            .ThenInclude(a => a.BlockedBy)
            .Include(f => f.BlockedBy)
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Slug)
            .ToListAsync(ct);

        var entries = await db.ContextEntries
            .Include(e => e.EntryTags)
            .ThenInclude(t => t.Tag)
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);

        var plans = MirrorPlanRenderer.BuildPlans(target, features, rows);
        plans.AddRange(MirrorPlanRenderer.BuildEntryPlans(target, entries, rows));

        foreach (var plan in plans)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plan.AbsolutePath)!);
            await File.WriteAllTextAsync(
                plan.AbsolutePath, ContentHash.Normalize(plan.Content), ContentHash.Utf8NoBom, ct);
        }

        return new PlanningExportResult(projectId, target, plans.Count);
    }

    private static void RefuseMirrorCollision(string projectRoot, string target)
    {
        var root = Path.GetFullPath(projectRoot);
        var backlog = Path.GetFullPath(Path.Combine(root, "BACKLOG"));
        var entries = Path.GetFullPath(Path.Combine(root, "ENTRIES"));
        if (PathGuard.PathsEqual(target, root) ||
            SitsUnder(target, backlog) || SitsUnder(target, entries))
        {
            throw PlanningException.ExportTargetInsideMirror(target);
        }
    }

    private static void RefuseNonEmptyTarget(string target)
    {
        if (File.Exists(target))
        {
            throw PlanningException.ExportTargetNotEmpty(target);
        }

        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            throw PlanningException.ExportTargetNotEmpty(target);
        }
    }

    private static bool SitsUnder(string candidate, string baseDirectory)
    {
        var prefix = baseDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? baseDirectory
            : baseDirectory + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return PathGuard.PathsEqual(candidate, baseDirectory) ||
               candidate.StartsWith(prefix, comparison);
    }
}
