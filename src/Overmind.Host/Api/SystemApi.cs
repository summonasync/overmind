using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Host.Api;

public static class SystemApi
{
    private const int MaxDirectoryEntries = 500;
    private static readonly SemaphoreSlim DialogLock = new(1, 1);
    private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(120);
    // MVP measure: spawning a shell dialog per request (powershell / osascript / zenity / kdialog per OS).
    public static RouteGroupBuilder MapSystemApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system");
        group.MapPost("/browse-folder", BrowseFolder);
        group.MapGet("/list-directory", ListDirectory);
        return group;
    }

    private static async Task<IResult> ListDirectory(string? path, OvermindDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return HttpErrors.BadRequest("Query parameter 'path' is required.");
        }

        if (!Path.IsPathRooted(path))
        {
            return HttpErrors.BadRequest($"'{path}' is not an absolute path.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return HttpErrors.BadRequest($"'{path}' is not a valid path: {ex.Message}");
        }

        if (!Directory.Exists(fullPath))
        {
            return HttpErrors.NotFound($"'{fullPath}' does not exist or is not a directory.");
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var projects = await db.Projects.AsNoTracking().ToListAsync(ct);
        var projectMap = projects.ToDictionary(
            p => NormalizePath(p.DiskPath),
            StringComparer.FromComparison(comparison));

        var normalizedRoot = NormalizePath(fullPath);
        var entries = new List<DirectoryEntryResponse>();
        var truncated = false;
        foreach (var directory in Directory.EnumerateDirectories(fullPath))
        {
            try
            {
                var attributes = File.GetAttributes(directory);
                if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    continue;
                }

                var normalized = NormalizePath(directory);
                projectMap.TryGetValue(normalized, out var project);
                entries.Add(new DirectoryEntryResponse(
                    Path.GetFileName(normalized),
                    normalized,
                    true,
                    project?.Id,
                    project?.Name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                entries.Add(new DirectoryEntryResponse(
                    Path.GetFileName(NormalizePath(directory)),
                    NormalizePath(directory),
                    false,
                    null,
                    null));
            }

            if (entries.Count >= MaxDirectoryEntries)
            {
                truncated = true;
                break;
            }
        }

        var ordered = entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Results.Ok(new ListDirectoryResponse(normalizedRoot, truncated, ordered));
    }

    internal static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length <= root.Length ? root : trimmed;
    }

    private static async Task<IResult> BrowseFolder()
    {
        if (OperatingSystem.IsLinux() && FindLinuxPicker() is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Folder picker unavailable",
                detail: "Install zenity or kdialog to enable the folder picker on Linux.");
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        }

        if (!await DialogLock.WaitAsync(TimeSpan.Zero))
        {
            return Results.StatusCode(StatusCodes.Status409Conflict);
        }

        try
        {
            var processStartInfo = CreateDialogProcess();

            using (var process = Process.Start(processStartInfo))
            {
                if (process is null)
                {
                    return Results.StatusCode(StatusCodes.Status501NotImplemented);
                }

                using var timeoutCts = new CancellationTokenSource(DialogTimeout);

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill();
                    return Results.NoContent();
                }

                var selectedPath = (await process.StandardOutput.ReadToEndAsync()).Trim();

                if (selectedPath.Length == 0)
                {
                    return Results.NoContent();
                }

                return Results.Ok(new BrowseFolderResponse(selectedPath));
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Folder picker unavailable",
                detail: ex.Message);
        }
        finally
        {
            DialogLock.Release();
        }
    }

    private static ProcessStartInfo CreateDialogProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Sta -NoProfile -NonInteractive -Command \"Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.FolderBrowserDialog; $d.Description = 'Select project folder'; if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Out.Write($d.SelectedPath) }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        if (OperatingSystem.IsLinux())
        {
            var picker = FindLinuxPicker();
            if (picker is null)
            {
                throw new InvalidOperationException("Neither zenity nor kdialog found on PATH.");
            }

            return new ProcessStartInfo
            {
                FileName = picker,
                Arguments = picker == "zenity"
                    ? "--file-selection --directory --title=\"Select project folder\""
                    : "--getexistingdirectory",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        return new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = "-e 'POSIX path of (choose folder with prompt \"Select project folder\")'",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static string? FindLinuxPicker()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var picker in new[] { "zenity", "kdialog" })
            {
                if (File.Exists(Path.Combine(directory, picker)))
                {
                    return picker;
                }
            }
        }

        return null;
    }
}
