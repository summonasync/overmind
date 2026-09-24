using Overmind.Core;

namespace Overmind.Infrastructure.Materialization;

internal static class PathGuard
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string ResolveUnderRoot(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment == ".."))
        {
            throw MaterializationException.Traversal(root, relativePath);
        }

        var fullRoot = Path.GetFullPath(root);

        string resolved;
        try
        {
            resolved = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw MaterializationException.Traversal(fullRoot, relativePath);
        }

        return GuardContained(fullRoot, resolved);
    }

    public static string ResolveAbsoluteUnderRoot(string root, string absolutePath)
    {
        if (!Path.IsPathFullyQualified(absolutePath))
        {
            throw MaterializationException.NotAbsolute(root, absolutePath);
        }

        string resolved;
        try
        {
            resolved = Path.GetFullPath(absolutePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw MaterializationException.OutsideRoot(root, absolutePath);
        }

        return GuardContained(Path.GetFullPath(root), resolved);
    }

    private static string GuardContained(string fullRoot, string resolved)
    {
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(rootWithSeparator, PathComparison))
        {
            throw MaterializationException.OutsideRoot(fullRoot, resolved);
        }

        return resolved;
    }

    public static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, PathComparison);

    public static IEqualityComparer<string> PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
