using System.Text.RegularExpressions;

namespace Overmind.Mcp.Registration;

public static partial class AgentFileConflictScanner
{
    public static IReadOnlyList<AgentFileConflict> Scan(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var managedStart = content.IndexOf(OnboardManagedBlock.StartMarker, StringComparison.Ordinal);
        var managedEnd = -1;
        if (managedStart >= 0)
        {
            var endIndex = content.IndexOf(
                OnboardManagedBlock.EndMarker,
                managedStart + OnboardManagedBlock.StartMarker.Length,
                StringComparison.Ordinal);
            managedEnd = endIndex >= 0 ? endIndex + OnboardManagedBlock.EndMarker.Length : -1;
        }

        var conflicts = new List<AgentFileConflict>();
        var lineNumber = 1;
        var lineStart = 0;
        while (lineStart <= content.Length)
        {
            var newlineIndex = content.IndexOf('\n', lineStart);
            var lineEnd = newlineIndex < 0 ? content.Length : newlineIndex;
            var line = content[lineStart..lineEnd];

            var intersectsManaged = managedStart >= 0 &&
                                    lineStart < managedEnd &&
                                    lineEnd > managedStart;

            if (!intersectsManaged && ContainsCeremonySignal(line))
            {
                conflicts.Add(new AgentFileConflict
                {
                    LineNumber = lineNumber,
                    Text = line.TrimEnd('\r'),
                });
            }

            if (newlineIndex < 0)
            {
                break;
            }

            lineStart = newlineIndex + 1;
            lineNumber++;
        }

        return conflicts;
    }

    private static bool ContainsCeremonySignal(string line) =>
        line.Contains("BACKLOG/", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("BACKLOG.md", StringComparison.OrdinalIgnoreCase) ||
        TaskWorkflowPattern().IsMatch(line);

    [GeneratedRegex(@"tasks/\d+-", RegexOptions.IgnoreCase)]
    private static partial Regex TaskWorkflowPattern();
}
