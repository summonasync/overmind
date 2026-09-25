using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningMaterializationStatusReader
{
    private readonly OvermindDbContext _db;
    private readonly PlanningPolicy _policy;
    private readonly IPlanningMaterializer _materializer;
    private readonly string _dataDirectory;

    public PlanningMaterializationStatusReader(
        OvermindDbContext db,
        PlanningPolicy policy,
        IPlanningMaterializer materializer,
        string dataDirectory)
    {
        _db = db;
        _policy = policy;
        _materializer = materializer;
        _dataDirectory = dataDirectory;
    }

    public async Task<PlanningMaterializationStatus> ReadAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw MaterializationException.MissingProject(projectId);
        var strategy = await _policy.ResolveMaterializationAsync(projectId, ct);
        var rows = await _db.PlanningMaterializedFiles
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.RelativePath)
            .ToListAsync(ct);
        var reason = AttemptReason(project);
        var run = ReportForLastAttempt(project);

        return new PlanningMaterializationStatus(
            projectId,
            project.LastMaterializedAt,
            project.LastMaterializationAttemptAt,
            reason,
            run,
            rows.Count,
            await StalePathsAsync(projectId, strategy, rows, run, ct),
            strategy,
            TargetDir(project, strategy));
    }

    private static string? AttemptReason(Project project) =>
        project.LastMaterializationAttemptAt is not { } attempt
            ? null
            : project.LastMaterializedAt is { } success && attempt <= success
                ? null
                : project.LastMaterializationError;

    private static MaterializationRunReport? ReportForLastAttempt(Project project)
    {
        var run = MaterializationRunJson.Deserialize(project.LastMaterializationRun);
        if (run is null || project.LastMaterializationAttemptAt is not { } attempt)
        {
            return run;
        }

        return run.FinishedAt >= attempt ? run : null;
    }

    private async Task<string[]> StalePathsAsync(
        Guid projectId,
        PlanningMaterialization strategy,
        List<PlanningMaterializedFile> rows,
        MaterializationRunReport? run,
        CancellationToken ct)
    {
        if (rows.Count == 0 || strategy == PlanningMaterialization.Off)
        {
            return [];
        }

        var handled = new HashSet<string>(StringComparer.Ordinal);
        if (run is not null)
        {
            foreach (var path in run.Written)
            {
                handled.Add(path);
            }

            foreach (var skip in run.Skipped)
            {
                handled.Add(skip.RelativePath);
            }
        }

        var drifted = run is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _materializer.DetectDriftAsync(projectId, ct))
                .Select(d => d.RelativePath)
                .ToHashSet(StringComparer.Ordinal);

        return rows
            .Where(row => !handled.Contains(row.RelativePath) || drifted.Contains(row.RelativePath))
            .Select(row => row.RelativePath)
            .ToArray();
    }

    private string TargetDir(Project project, PlanningMaterialization strategy) =>
        strategy == PlanningMaterialization.Archive
            ? PlanningArchive.Root(_dataDirectory, project)
            : Path.Combine(project.DiskPath, "BACKLOG");
}
