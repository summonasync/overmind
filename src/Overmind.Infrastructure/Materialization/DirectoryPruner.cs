namespace Overmind.Infrastructure.Materialization;

internal static class DirectoryPruner
{
    public static void PruneEmptyDirectories(string filePath, string root)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fullRoot = Path.GetFullPath(root);

        while (directory is not null &&
               !PathGuard.PathsEqual(directory, fullRoot) &&
               directory.StartsWith(
                   fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar,
                   OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
            {
                break;
            }

            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
