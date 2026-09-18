using System.Globalization;

namespace Overmind.Mcp.Registration;

public sealed class ConfigBackupTracker
{
    private const string TimestampFormat = "yyyyMMddHHmmss";
    private readonly HashSet<string> _backedUpFiles = new(StringComparer.Ordinal);

    public string? EnsureBackup(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(filePath);
        if (!_backedUpFiles.Add(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        var stamp = DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var backupPath = $"{fullPath}.{stamp}.bak";
        var collision = 0;
        while (File.Exists(backupPath))
        {
            collision++;
            backupPath = $"{fullPath}.{stamp}-{collision}.bak";
        }

        File.Copy(fullPath, backupPath);
        return backupPath;
    }
}
