using System.Text;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

public static class MirrorGitHygiene
{
    private const string BacklogPattern = "BACKLOG/";
    private const string EntriesPattern = "ENTRIES/";

    private static readonly SemaphoreSlim ExcludeWriteGate = new(1, 1);

    public static async Task<PlanningGitHygieneOutcome> EnsureAsync(string targetRoot, CancellationToken ct)
    {
        var target = Path.GetFullPath(targetRoot);
        var repositoryRoot = FindRepositoryRoot(target);
        if (repositoryRoot is null)
        {
            return new PlanningGitHygieneOutcome("no-repo");
        }

        await ExcludeWriteGate.WaitAsync(ct);
        try
        {
            var excludePath = Path.Combine(repositoryRoot, ".git", "info", "exclude");
            Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);
            var existing = File.Exists(excludePath)
                ? await File.ReadAllTextAsync(excludePath, ct)
                : string.Empty;

            var present = existing
                .Split('\n')
                .Select(line => line.Trim())
                .ToHashSet(StringComparer.Ordinal);
            var missing = MirrorPatterns(repositoryRoot, target)
                .Where(pattern => !present.Contains(pattern))
                .ToArray();
            if (missing.Length == 0)
            {
                return new PlanningGitHygieneOutcome("already-present", repositoryRoot);
            }

            var builder = new StringBuilder();
            if (existing.Length > 0 && !existing.EndsWith('\n'))
            {
                builder.Append('\n');
            }

            foreach (var pattern in missing)
            {
                builder.Append(pattern).Append('\n');
            }

            await File.AppendAllTextAsync(excludePath, builder.ToString(), ContentHash.Utf8NoBom, ct);
            return new PlanningGitHygieneOutcome("ensured", repositoryRoot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PlanningGitHygieneOutcome("failed", repositoryRoot, ex.Message);
        }
        finally
        {
            ExcludeWriteGate.Release();
        }
    }

    private static string? FindRepositoryRoot(string start)
    {
        for (var current = start; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> MirrorPatterns(string repositoryRoot, string target)
    {
        var relative = Path.GetRelativePath(repositoryRoot, target);
        if (relative is "." or "")
        {
            return [BacklogPattern, EntriesPattern];
        }

        var prefix = relative.Replace('\\', '/');
        return [$"{prefix}/{BacklogPattern}", $"{prefix}/{EntriesPattern}"];
    }
}
