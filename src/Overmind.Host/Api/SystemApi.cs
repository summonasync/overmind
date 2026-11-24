using Microsoft.EntityFrameworkCore;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using System.Text;

namespace Overmind.Host.Api;

public static class SystemApi
{
    private const int MaxDirectoryEntries = 500;

    private static readonly string[] RealFilesystemTypes =
    [
        "ext2", "ext3", "ext4", "btrfs", "bcachefs", "xfs", "zfs", "f2fs",
        "ntfs", "ntfs3", "vfat", "exfat", "udf", "fuseblk",
    ];

    public static RouteGroupBuilder MapSystemApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system");
        group.MapGet("/browse-roots", BrowseRoots);
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
        var accessible = true;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(fullPath))
            {
                if (IsHiddenOnUnix(directory))
                {
                    continue;
                }

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
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            accessible = false;
        }

        var ordered = entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Results.Ok(new ListDirectoryResponse(normalizedRoot, truncated, ordered, accessible));
    }

    private static bool IsHiddenOnUnix(string path) =>
        !OperatingSystem.IsWindows() && Path.GetFileName(path).StartsWith('.');

    internal static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length <= root.Length ? root : trimmed;
    }

    private static async Task<IResult> BrowseRoots(AppSettingsService settings, CancellationToken ct)
    {
        var roots = new List<BrowseRootEntry>();
        AddHomeRoot(roots);
        AddVolumeRoots(roots);

        var treeRoots = await SystemRootsApi.ReadRootsAsync(settings, ct);
        foreach (var root in treeRoots)
        {
            AddBrowseRoot(roots, root, "treeRoot");
        }

        return Results.Ok(new BrowseRootsResponse(roots.ToArray()));
    }

    private static void AddHomeRoot(List<BrowseRootEntry> roots) =>
        AddBrowseRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "home", "home");

    private static void AddVolumeRoots(List<BrowseRootEntry> roots)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                AddBrowseRoot(roots, drive.Name, "drive", GetDriveLabel(drive));
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            foreach (var volume in ListMacVolumes())
            {
                AddBrowseRoot(roots, volume, "volume");
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            foreach (var volume in ListLinuxVolumes())
            {
                AddBrowseRoot(roots, volume, "volume");
            }
        }
    }

    private static string GetDriveLabel(DriveInfo drive) =>
        string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel;

    private static IReadOnlyList<string> ListMacVolumes()
    {
        try
        {
            return Directory.EnumerateDirectories("/Volumes").ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ListLinuxVolumes()
    {
        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var volumes = new List<string>();
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var fields = line.Split(' ');
                if (fields.Length < 3 || !fields[1].StartsWith('/') || !RealFilesystemTypes.Contains(fields[2]))
                {
                    continue;
                }

                var mountPoint = UnescapeMountPath(fields[1]);
                if (seen.Add(mountPoint))
                {
                    volumes.Add(mountPoint);
                }
            }

            return volumes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string UnescapeMountPath(string raw)
    {
        var decoded = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length;)
        {
            var decodedChar = raw[i] == '\\' && i + 4 <= raw.Length
                ? raw.Substring(i + 1, 3) switch
                {
                    "040" => ' ',
                    "011" => '\t',
                    "012" => '\n',
                    "134" => '\\',
                    _ => null,
                }
                : null;

            if (decodedChar is { } ch)
            {
                decoded.Append(ch);
                i += 4;
            }
            else
            {
                decoded.Append(raw[i++]);
            }
        }

        return decoded.ToString();
    }

    private static void AddBrowseRoot(List<BrowseRootEntry> roots, string path, string kind, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        var normalized = NormalizePath(path);
        if (roots.Any(existing => existing.Path.Equals(normalized, PathComparison)))
        {
            return;
        }

        var resolved = name ?? Path.GetFileName(normalized);
        roots.Add(new BrowseRootEntry(
            string.IsNullOrEmpty(resolved) ? normalized : resolved,
            normalized,
            kind));
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
