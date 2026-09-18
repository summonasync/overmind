namespace Overmind.Mcp.Registration;

public static class StableInstallLinkResolver
{
    public static readonly string StableLinkRelativePath = Path.Combine("apps", "overmind", "mcp", "overmind-mcp");

    public static string ResolvePreferredBinaryPath(string runningBinaryPath, AgentPathEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runningBinaryPath);
        ArgumentNullException.ThrowIfNull(environment);

        var realPath = ResolveFinalTarget(runningBinaryPath) ?? Path.GetFullPath(runningBinaryPath);
        var linkPath = Path.Combine(environment.HomeDirectory, StableLinkRelativePath);

        foreach (var candidate in LinkCandidates(linkPath))
        {
            var linkTarget = ResolveFinalTarget(candidate);
            if (linkTarget is not null && PathEquals(linkTarget, realPath))
            {
                return candidate;
            }
        }

        return realPath;
    }

    private static IEnumerable<string> LinkCandidates(string linkPath)
    {
        yield return linkPath;

        if (OperatingSystem.IsWindows())
        {
            yield return linkPath + ".exe";
        }
    }

    private static string? ResolveFinalTarget(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full) && !Directory.Exists(full))
            {
                return null;
            }

            // ResolveLinkTarget only resolves a leaf link; a symlinked directory component
            // (~/apps/overmind -> overmind-<ver>) needs the whole chain walked.
            var root = Path.GetPathRoot(full)!;
            var current = root;
            foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                var target = ResolveLink(current);
                if (target is not null)
                {
                    current = target;
                }
            }

            return current;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ResolveLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (!info.Exists)
        {
            return null;
        }

        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
