using System.Text.Json;

namespace Overmind.Mcp;

public static class SessionIdentityFiles
{
    public static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

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
