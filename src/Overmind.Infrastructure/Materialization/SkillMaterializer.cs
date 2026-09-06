using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Materialization;

public sealed class SkillMaterializer : IMaterializer
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly OvermindDbContext _db;
    private readonly PlatformDirectoryMap _platforms;

    public SkillMaterializer(OvermindDbContext db, PlatformDirectoryMap platforms)
    {
        _db = db;
        _platforms = platforms;
    }

    public async Task<MaterializationReport> MaterializeAsync(Guid projectId, Guid kitId, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw MaterializationException.MissingProject(projectId);
        var kit = await LoadKitAsync(kitId, ct)
            ?? throw MaterializationException.MissingKit(kitId);

        var plans = BuildPlans(project, kit);

        var tracked = await _db.MaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        foreach (var plan in plans)
        {
            if (!File.Exists(plan.AbsolutePath) && !Directory.Exists(plan.AbsolutePath))
            {
                continue;
            }

            var owned = tracked.Any(m =>
                m.KitId == kitId &&
                PathGuard.PathsEqual(PathGuard.ResolveUnderRoot(project.DiskPath, m.RelativePath), plan.AbsolutePath));

            if (!owned)
            {
                throw MaterializationException.Overwrite(plan.AbsolutePath);
            }
        }

        var written = new List<string>();
        string? failedPath = null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var plan in plans)
            {
                failedPath = plan.AbsolutePath;
                Directory.CreateDirectory(Path.GetDirectoryName(plan.AbsolutePath)!);
                await File.WriteAllTextAsync(plan.AbsolutePath, plan.NormalizedContent, Utf8NoBom, ct);
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
                TryDelete(path);
            }

            throw MaterializationException.IoFailure(failedPath ?? "database", ex);
        }

        return new MaterializationReport(
            projectId,
            kitId,
            plans.Select(p => new MaterializedFileInfo(p.Platform, p.RelativePath, p.Sha256)).ToArray());
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
                    PruneEmptyDirectories(absolute, project.DiskPath);
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
            removed.Select(r => new MaterializedFileInfo(r.Platform, r.RelativePath, r.Sha256)).ToArray());
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
        var platforms = skills.SelectMany(e => e.SkillFiles).Select(f => f.Platform).Distinct().ToList();

        var plans = new List<FilePlan>();

        foreach (var skill in skills)
        {
            var slug = Slugify(skill.Title);
            foreach (var file in skill.SkillFiles)
            {
                PathGuard.ResolveUnderRoot(project.DiskPath, file.RelativePath);
                var relative = $"{_platforms.GetSkillsDir(file.Platform)}/{slug}/{file.RelativePath}";
                plans.Add(FilePlan.Create(project.DiskPath, file.Platform, relative, file.Content));
            }
        }

        foreach (var platform in platforms)
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

    private static string BuildBootstrap(Guid projectId, IReadOnlyList<ContextEntry> gates, IReadOnlyList<ContextEntry> guards)
    {
        var builder = new StringBuilder();
        builder.Append("# Overmind Bootstrap\n\n");
        builder.Append("This project is managed by Overmind. At the start of every session, read the project context through Overmind:\n\n");
        builder.Append($"- Gates: ctx://projects/{projectId}/gates\n");
        builder.Append($"- Guards: ctx://projects/{projectId}/guards\n");

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

    private static string Slugify(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var c in title.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c) && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.Length == 0 ? "skill" : builder.ToString();
    }

    private static void PruneEmptyDirectories(string filePath, string root)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fullRoot = Path.GetFullPath(root);

        while (directory is not null &&
               !PathGuard.PathsEqual(directory, fullRoot) &&
               directory.StartsWith(
                   fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar,
                   OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
            {
                break;
            }

            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record FilePlan(
        AgentPlatform Platform,
        string RelativePath,
        string AbsolutePath,
        string Content,
        string Sha256)
    {
        public string NormalizedContent => Content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        public static FilePlan Create(string root, AgentPlatform platform, string relativePath, string content)
        {
            var normalized = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
            var sha256 = Convert.ToHexString(SHA256.HashData(Utf8NoBom.GetBytes(normalized)));

            return new FilePlan(platform, relativePath, PathGuard.ResolveUnderRoot(root, relativePath), content, sha256);
        }
    }
}
