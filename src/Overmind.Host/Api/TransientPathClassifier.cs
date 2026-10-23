namespace Overmind.Host.Api;

public static class TransientPathClassifier
{
    private static readonly string[] FixedRoots = ["/tmp", "/var/tmp", "/run"];

    public static bool IsTransient(string path)
    {
        foreach (var root in Roots())
        {
            if (IsUnderRoot(path, root))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Roots()
    {
        foreach (var root in FixedRoots)
        {
            yield return TrimTrailingSeparators(root);
        }

        var tempDir = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tempDir) && Path.IsPathRooted(tempDir))
        {
            yield return TrimTrailingSeparators(tempDir);
        }

        yield return TrimTrailingSeparators(Path.GetTempPath());
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return path.Equals(root, comparison)
            || (path.Length > root.Length
                && path.StartsWith(root, comparison)
                && IsSeparator(path[root.Length]));
    }

    private static bool IsSeparator(char c) =>
        c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

    private static string TrimTrailingSeparators(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? path : trimmed;
    }
}
