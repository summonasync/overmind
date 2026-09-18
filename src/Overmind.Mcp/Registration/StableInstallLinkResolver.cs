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
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);
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

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
