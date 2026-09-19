using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class BacklogImportSeeder
{
    private readonly OvermindDbContext _db;
    private readonly PlanningPolicy _policy;

    public BacklogImportSeeder(OvermindDbContext db, PlanningPolicy policy)
    {
        _db = db;
        _policy = policy;
    }

    public async Task<BacklogImportSeedResult> SeedAsync(
        Guid projectId, Guid featureId, IReadOnlyList<string> sourcePaths, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return new BacklogImportSeedResult(0, PlanningMaterialization.Off);
        }

        var strategy = await _policy.ResolveMaterializationAsync(projectId, ct);
        if (strategy != PlanningMaterialization.On || sourcePaths.Count == 0)
        {
            return new BacklogImportSeedResult(0, strategy);
        }

        var rows = await _db.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var tracked = 0;
        var usedTaskOrders = new HashSet<int>();
        foreach (var sourcePath in sourcePaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            var relative = TryMakeRelative(project.DiskPath, sourcePath);
            if (relative is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(relative);
            if (BacklogNaming.TryParseTaskOrder(fileName, out var order, out var suffix) &&
                suffix == '\0' &&
                !usedTaskOrders.Add(order))
            {
                continue;
            }

            var existing = rows.FirstOrDefault(r => PathGuard.PathsEqual(r.RelativePath, relative));
            if (existing is not null)
            {
                existing.AssignFeature(featureId);
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(sourcePath, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var row = PlanningMaterializedFile.Create(
                projectId, featureId, relative, Convert.ToHexString(SHA256.HashData(bytes)));
            _db.PlanningMaterializedFiles.Add(row);
            rows.Add(row);
            tracked++;
        }

        return new BacklogImportSeedResult(tracked, strategy);
    }

    private static string? TryMakeRelative(string root, string absolutePath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(absolutePath));
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relative == "." || Path.IsPathRooted(relative) || segments.Any(segment => segment == ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }
}
