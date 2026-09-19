using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Serilog;

namespace Overmind.Host;

internal static class BacklogImportCommand
{
    public static async Task TryRunAsync(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var backlogPath = FlagValue(args, "--import-backlog");
        if (backlogPath is null)
        {
            return;
        }

        var backlogDir = BacklogImporter.ResolveBacklogDirectory(backlogPath);
        if (backlogDir is null)
        {
            Log.Error(
                "--import-backlog: {Path} is neither a repository root containing a BACKLOG folder nor a BACKLOG folder itself; import skipped",
                backlogPath);
            return;
        }

        try
        {
            var db = services.GetRequiredService<OvermindDbContext>();
            var repoRoot = Path.GetFullPath(Path.GetDirectoryName(backlogDir)!);
            var project = await ResolveProjectAsync(db, repoRoot, FlagValue(args, "--project"), ct);
            if (project is null)
            {
                return;
            }

            var importer = services.GetRequiredService<BacklogImporter>();
            var report = await importer.ImportAsync(new BacklogImportRequest(backlogPath, project.Id), ct);

            foreach (var line in BacklogImportReportText.Lines(report, backlogPath))
            {
                Log.Information("{Line}", line);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Backlog import from {Path} failed; continuing startup", backlogPath);
        }
    }

    private static async Task<Project?> ResolveProjectAsync(
        OvermindDbContext db, string repoRoot, string? selector, CancellationToken ct)
    {
        var (project, error) = await ResolveProjectResultAsync(db, repoRoot, selector, ct);
        if (error is not null)
        {
            Log.Error("{ImportError}", error);
        }

        return project;
    }

    internal static async Task<(Project? Project, string? Error)> ResolveProjectResultAsync(
        OvermindDbContext db, string repoRoot, string? selector, CancellationToken ct)
    {
        if (selector is not null)
        {
            if (Guid.TryParse(selector, out var id))
            {
                var byId = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
                if (byId is not null)
                {
                    return (byId, null);
                }

                return (null, $"--import-backlog: no registered project has id {id}");
            }

            var byName = await db.Projects.Where(p => p.Name == selector).ToListAsync(ct);
            if (byName.Count == 1)
            {
                return (byName[0], null);
            }

            return (
                null,
                $"--import-backlog: project selector '{selector}' matched {byName.Count} projects ({string.Join(", ", byName.Select(p => p.Name))})");
        }

        var matches = (await db.Projects.ToListAsync(ct))
            .Where(p => PathsEqual(Path.GetFullPath(p.DiskPath), repoRoot))
            .ToList();

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }

        return (
            null,
            $"--import-backlog: expected exactly one project with disk path {repoRoot}, found {matches.Count} ({string.Join(", ", matches.Select(p => p.Name))})");
    }

    internal static string? FlagValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
