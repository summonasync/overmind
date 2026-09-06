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

        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(rootWithSeparator, PathComparison))
        {
            throw MaterializationException.Traversal(fullRoot, relativePath);
        }

        return resolved;
    }

    public static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, PathComparison);

    public static IEqualityComparer<string> PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
