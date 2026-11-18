using System.Globalization;
using System.Text.Json;

namespace Overmind.Mcp;

public static class SessionIdentityFiles
{
    private const string ParentPrefix = "parent-";
    private const string JsonSuffix = ".json";

    public static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    // A parent-<pid>.json belongs to a parent that is gone once that process dies, and nothing reads
    // it back; workspace-<key>.json is the restore source and stays — its key is a hash, so no file
    // here can be traced back to a workspace path to be judged stale.
    public static int PruneStaleParentSnapshots(
        string? configuredRoot = null, Func<int, bool>? parentIsAlive = null)
    {
        var directory = IdentityDirectory(configuredRoot);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var isAlive = parentIsAlive ?? ParentProcessIsAlive;
        var pruned = 0;
        foreach (var file in Directory.GetFiles(directory, $"{ParentPrefix}*{JsonSuffix}"))
        {
            if (ParentPidOf(Path.GetFileName(file)) is not { } pid || isAlive(pid))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                pruned++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Console.Error.WriteLine(
                    $"[overmind-mcp] WARN: stale identity file {Path.GetFileName(file)} not pruned: {ex.Message}");
            }
        }

        return pruned;
    }

    private static int? ParentPidOf(string fileName)
    {
        var digits = fileName[ParentPrefix.Length..^JsonSuffix.Length];
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0
            ? pid
            : null;
    }

    private static bool ParentProcessIsAlive(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public static string IdentityDirectory(string? configuredRoot) =>
        Path.Combine(
            configuredRoot
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".overmind"),
            "identity");

    public static void Write(string targetFile, SessionIdentitySnapshot snapshot)
    {
        var temp = $"{targetFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, PayloadOptions));
            File.Move(temp, targetFile, true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: session identity file {Path.GetFileName(targetFile)} not updated: {ex.Message}");
            TryCleanup(temp, targetFile);
        }
    }

    private static void TryCleanup(string temp, string targetFile)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception cleanup) when (cleanup is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: session identity temp file cleanup failed for " +
                $"{Path.GetFileName(targetFile)}: {cleanup.Message}");
        }
    }
}
