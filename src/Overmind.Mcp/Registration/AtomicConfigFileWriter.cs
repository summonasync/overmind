namespace Overmind.Mcp.Registration;

internal static class AtomicConfigFileWriter
{
    public static async Task WriteAsync(string filePath, string content, ConfigBackupTracker backupTracker, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(backupTracker);

        backupTracker.EnsureBackup(filePath, cancellationToken);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
