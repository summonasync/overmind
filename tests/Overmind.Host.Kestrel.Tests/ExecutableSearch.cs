namespace Overmind.Host.Kestrel.Tests;

public static class ExecutableSearch
{
    public static string? Find(
        string? forcedOverride,
        IReadOnlyList<string> names,
        IReadOnlyList<string> extraDirectories)
    {
        if (forcedOverride is { Length: > 0 })
        {
            return File.Exists(forcedOverride) ? forcedOverride : null;
        }

        return PathDirectories()
            .Concat(extraDirectories)
            .SelectMany(directory => names.Select(name => Path.Combine(directory, name)))
            .FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> PathDirectories() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
