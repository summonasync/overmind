using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Materialization;

public sealed class SkillMaterializer : IMaterializer
{
    private readonly OvermindDbContext _db;
    private readonly PlatformDirectoryMap _platforms;

    public SkillMaterializer(OvermindDbContext db, PlatformDirectoryMap platforms)
    {
        _db = db;
        _platforms = platforms;
    }

    public async Task<MaterializationReport> MaterializeAsync(
        Guid projectId, Guid kitId, bool force = false, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw MaterializationException.MissingProject(projectId);
        var kit = await LoadKitAsync(kitId, ct)
            ?? throw MaterializationException.MissingKit(kitId);

        var plans = BuildPlans(project, kit);

        var tracked = await _db.MaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var warnings = new List<string>();

        foreach (var plan in plans)
        {
            if (!File.Exists(plan.AbsolutePath) && !Directory.Exists(plan.AbsolutePath))
            {
                continue;
            }

            var owned = tracked.Any(m =>
                m.KitId == kitId &&
                PathGuard.PathsEqual(PathGuard.ResolveUnderRoot(project.DiskPath, m.RelativePath), plan.AbsolutePath));

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

        await GuardLocalEditsAsync(plans, tracked, project.DiskPath, kitId, force, warnings, ct);

        var written = new List<string>();
        string? failedPath = null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var plan in plans)
            {
                failedPath = plan.AbsolutePath;
                Directory.CreateDirectory(Path.GetDirectoryName(plan.AbsolutePath)!);
                await File.WriteAllTextAsync(plan.AbsolutePath, plan.NormalizedContent, ContentHash.Utf8NoBom, ct);
                written.Add(plan.AbsolutePath);

                var existingRow = tracked.FirstOrDefault(m =>
                    m.KitId == kitId &&
                    PathGuard.PathsEqual(PathGuard.ResolveUnderRoot(project.DiskPath, m.RelativePath), plan.AbsolutePath));

                if (existingRow is not null)
                {
                    existingRow.Refresh(plan.Sha256);
                }
                else
                {
                    _db.MaterializedFiles.Add(MaterializedFile.Create(
                        projectId, kitId, plan.Platform, plan.RelativePath, plan.Sha256));
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
                DirectoryPruner.TryDelete(path);
            }

            throw MaterializationException.IoFailure(failedPath ?? "database", ex);
        }

        return new MaterializationReport(
            projectId,
            kitId,
            plans.Select(p => new MaterializedFileInfo(p.Platform, p.RelativePath, p.Sha256)).ToArray(),
            warnings);
    }

    // Kit files land in the user's project workspace only — there is no archive root, so there is
    // no second copy to prove ownership against. Kit files carry no managed block, so the
    // whole-file hash decides: a write loses hand edits only when the row proves the file is
    // tracked, the disk hash drifted from the row, and the plan differs from what is on disk.
    private static async Task GuardLocalEditsAsync(
        List<FilePlan> plans, List<MaterializedFile> tracked, string diskPath, Guid kitId, bool force,
        List<string> warnings, CancellationToken ct)
    {
        List<string>? locallyModified = null;

        foreach (var plan in plans)
        {
            var row = tracked.FirstOrDefault(m =>
                m.KitId == kitId &&
                PathGuard.PathsEqual(PathGuard.ResolveUnderRoot(diskPath, m.RelativePath), plan.AbsolutePath));
            if (row is null || !File.Exists(plan.AbsolutePath))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(plan.AbsolutePath, ct);
            var diskHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (string.Equals(diskHash, row.Sha256, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(diskHash, plan.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (force)
            {
                warnings.Add($"Overwrote locally modified file: {plan.AbsolutePath}");
                continue;
            }

            (locallyModified ??= []).Add(plan.AbsolutePath);
        }

        if (locallyModified is not null)
        {
            throw MaterializationException.LocalEdits(locallyModified);
        }
    }

    public async Task<MaterializationReport> DematerializeAsync(Guid projectId, Guid kitId, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw MaterializationException.MissingProject(projectId);

        var rows = await _db.MaterializedFiles
            .Where(m => m.ProjectId == projectId && m.KitId == kitId)
            .ToListAsync(ct);

        var removed = new List<MaterializedFile>();
        string? failedPath = null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in rows)
            {
                var absolute = PathGuard.ResolveUnderRoot(project.DiskPath, row.RelativePath);
                failedPath = absolute;

                if (File.Exists(absolute))
                {
                    File.Delete(absolute);
                    DirectoryPruner.PruneEmptyDirectories(absolute, project.DiskPath);
                }

                _db.MaterializedFiles.Remove(row);
                removed.Add(row);
            }

            failedPath = null;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not MaterializationException and not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            throw MaterializationException.IoFailure(failedPath ?? "database", ex);
        }

        return new MaterializationReport(
            projectId,
            kitId,
            removed.Select(r => new MaterializedFileInfo(r.Platform, r.RelativePath, r.Sha256)).ToArray(),
            []);
    }

    public async Task<IReadOnlyList<DriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw MaterializationException.MissingProject(projectId);

        var rows = await _db.MaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var items = new List<DriftItem>();
        foreach (var row in rows)
        {
            var absolute = PathGuard.ResolveUnderRoot(project.DiskPath, row.RelativePath);

            if (!File.Exists(absolute))
            {
                items.Add(new DriftItem(projectId, row.KitId, row.Platform, row.RelativePath, DriftKind.Missing));
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(absolute, ct);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new DriftItem(projectId, row.KitId, row.Platform, row.RelativePath, DriftKind.Modified));
            }
        }

        return items;
    }

    private async Task<SkillKit?> LoadKitAsync(Guid kitId, CancellationToken ct)
    {
        return await _db.SkillKits
            .Include(k => k.SkillKitEntries)
            .ThenInclude(e => e.Entry)
            .ThenInclude(e => e.SkillFiles)
            .FirstOrDefaultAsync(k => k.Id == kitId, ct);
    }

    private List<FilePlan> BuildPlans(Project project, SkillKit kit)
    {
        var entries = kit.SkillKitEntries.Select(e => e.Entry).ToList();
        var skills = entries.Where(e => e.Type == EntryType.Skill).ToList();
        var gates = entries.Where(e => e.Type == EntryType.Gate).ToList();
        var guards = entries.Where(e => e.Type == EntryType.Guard).ToList();

        var plans = new List<FilePlan>();
        var targeted = new List<AgentPlatform>();

        foreach (var skill in skills)
        {
            var slug = Slug.From(skill.Title, "skill");
            foreach (var file in skill.SkillFiles)
            {
                PathGuard.ResolveUnderRoot(project.DiskPath, file.RelativePath);

                if (file.Platform is { } platform)
                {
                    AddSkillPlan(project, plans, platform, slug, file);
                    TrackPlatform(targeted, platform);
                    continue;
                }

                foreach (var agnosticPlatform in _platforms.SkillsDirs.Keys)
                {
                    AddSkillPlan(project, plans, agnosticPlatform, slug, file);
                    TrackPlatform(targeted, agnosticPlatform);
                }
            }
        }

        foreach (var platform in targeted)
        {
            var relative = $"{_platforms.GetSkillsDir(platform)}/overmind-bootstrap/SKILL.md";
            plans.Add(FilePlan.Create(project.DiskPath, platform, relative, BuildBootstrap(project.Id, gates, guards)));
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

    private void AddSkillPlan(Project project, List<FilePlan> plans, AgentPlatform platform, string slug, SkillFile file)
    {
        var relative = $"{_platforms.GetSkillsDir(platform)}/{slug}/{file.RelativePath}";
        plans.Add(FilePlan.Create(project.DiskPath, platform, relative, file.Content));
    }

    private static void TrackPlatform(List<AgentPlatform> targeted, AgentPlatform platform)
    {
        if (!targeted.Contains(platform))
        {
            targeted.Add(platform);
        }
    }

    private static string BuildBootstrap(Guid projectId, IReadOnlyList<ContextEntry> gates, IReadOnlyList<ContextEntry> guards)
    {
        var builder = new StringBuilder();
        builder.Append("# Overmind Bootstrap\n\n");
        builder.Append("This project is managed by Overmind. At the start of every session, read the project context through Overmind:\n\n");
        builder.Append($"- Gates: overmind://projects/{projectId}/gates\n");
        builder.Append($"- Guards: overmind://projects/{projectId}/guards\n");

        if (gates.Count > 0)
        {
            builder.Append("\n## Gates\n");
            foreach (var gate in gates)
            {
                builder.Append($"\n### {gate.Title}\n\n{gate.Content}\n");
            }
        }

        if (guards.Count > 0)
        {
            builder.Append("\n## Guards\n");
            foreach (var guard in guards)
            {
                builder.Append($"\n### {guard.Title}\n\n{guard.Content}\n");
            }
        }

        return builder.ToString();
    }

    private sealed record FilePlan(
        AgentPlatform Platform,
        string RelativePath,
        string AbsolutePath,
        string Content,
        string Sha256)
    {
        public string NormalizedContent => ContentHash.Normalize(Content);

        public static FilePlan Create(string root, AgentPlatform platform, string relativePath, string content)
        {
            var sha256 = ContentHash.Sha256(content);

            return new FilePlan(platform, relativePath, PathGuard.ResolveUnderRoot(root, relativePath), content, sha256);
        }
    }
}
