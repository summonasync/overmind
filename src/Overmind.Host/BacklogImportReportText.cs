using System.Text;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host;

internal static class BacklogImportReportText
{
    public static IReadOnlyList<string> Lines(BacklogImportReport report, string backlogPath)
    {
        var lines = new List<string>
        {
            $"Imported planning from {backlogPath}: {report.FeaturesCreated + report.FeaturesUpdated} features ({report.FeaturesCreated} new), {report.ArtifactsCreated + report.ArtifactsUpdated} artifacts, {report.Skipped.Count} skipped",
        };
        lines.AddRange(report.Skipped);
        lines.AddRange(report.NewKeys.Select(key => $"new key: {key.Name} ({key.Color})"));
        lines.AddRange(report.Preserved);
        lines.AddRange(report.CreatedFallbacks);
        if (report.NextStep is not null)
        {
            lines.Add(report.NextStep);
        }

        return lines;
    }

    public static string Text(BacklogImportReport report, string backlogPath)
    {
        var builder = new StringBuilder();
        foreach (var line in Lines(report, backlogPath))
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}
