using System.Globalization;
using System.Text.Json;

namespace Overmind.Mcp;

public static class SessionIdentityFiles
{
    private const string ParentPrefix = "parent-";
    private const string JsonSuffix = ".json";
    private const string WriteTempGlob = "*.json.*.tmp";

    public static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    // A *.json.<guid>.tmp is the atomic write's own crash debris: the move renames it away whenever
    // the writer survives, and TryCleanup only runs on a caught exception, so a kill between write and
    // move strands it. Age is what separates that from a live writer mid-flight, hence the grace.
    private static readonly TimeSpan WriteTempGrace = TimeSpan.FromMinutes(2);

    // A parent-<pid>.json belongs to a parent that is gone once that process dies, and nothing reads
    // it back. workspace-<key>.json is deliberately never pruned: the key is a hash of the git root and
    // the file names no path, so liveness cannot be judged from disk, and age is not staleness for a
    // workspace the user opens once a quarter — that stamp IS the restore source.
    public static int PruneStaleIdentityFiles(
        string? configuredRoot = null,
        Func<int, bool>? parentIsAlive = null,
        TimeSpan? writeTempGrace = null)
    {
        var directory = IdentityDirectory(configuredRoot);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var isAlive = parentIsAlive ?? ParentProcessIsAlive;
        var grace = writeTempGrace ?? WriteTempGrace;
        var pruned = 0;
        foreach (var file in Directory.GetFiles(directory, $"{ParentPrefix}*{JsonSuffix}"))
        {
            if (ParentPidOf(Path.GetFileName(file)) is not { } pid || isAlive(pid))
            {
                continue;
            }

            if (TryPrune(file, "stale identity file"))
            {
                pruned++;
            }
        }

        foreach (var file in Directory.GetFiles(directory, WriteTempGlob))
        {
            if (WriteTempIsInFlight(file, grace))
            {
                continue;
            }

            if (TryPrune(file, "identity write temp"))
            {
                pruned++;
            }
        }

        return pruned;
    }

    private static bool WriteTempIsInFlight(string file, TimeSpan grace)
    {
        try
        {
            return DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < grace;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return true;
        }
    }

    private static bool TryPrune(string file, string what)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Console.Error.WriteLine(
                $"[overmind-mcp] WARN: {what} {Path.GetFileName(file)} not pruned: {ex.Message}");
            return false;
        }
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
