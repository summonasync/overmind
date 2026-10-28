namespace Overmind.Host.Api;

public static class WorktreeProbe
{
    public static WorktreeOrigin? Probe(string startDirectory)
    {
        var current = FullPath(startDirectory);
        while (true)
        {
            var gitPath = Path.Combine(current, ".git");
            if (Directory.Exists(gitPath))
            {
                return null;
            }

            if (File.Exists(gitPath))
            {
                return MainRoot(current, gitPath) is { } mainRoot
                    ? new WorktreeOrigin(current, mainRoot)
                    : null;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null)
            {
                return null;
            }

            current = FullPath(parent);
        }
    }

    private static string? MainRoot(string worktreeRoot, string gitFilePath)
    {
        string content;
        try
        {
            content = File.ReadAllText(gitFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }

        var pointer = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith("gitdir:", StringComparison.Ordinal));
        var value = pointer?["gitdir:".Length..].Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        string gitDir;
        try
        {
            gitDir = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(worktreeRoot, value));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException)
        {
            return null;
        }

        var normalized = gitDir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var marker = $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}worktrees{Path.DirectorySeparatorChar}";
        var markerIndex = normalized.LastIndexOf(marker, StringComparison.Ordinal);
        return markerIndex < 0 ? null : normalized[..markerIndex];
    }

    private static string FullPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
