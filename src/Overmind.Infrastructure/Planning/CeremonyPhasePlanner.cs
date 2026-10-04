using System.Globalization;
using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public static class CeremonyPhasePlanner
{
    public const int ExcerptCap = 500;
    public const string Ellipsis = "…";

    public static string DeriveLabel(
        PlanningFeatureStatus status,
        PlanningArtifactStatus? specStatus,
        PlanningArtifactStatus? planStatus,
        int taskTotal)
    {
        if (status == PlanningFeatureStatus.Done)
        {
            return "done";
        }

        if (status == PlanningFeatureStatus.Review)
        {
            return "review";
        }

        if (status == PlanningFeatureStatus.InProgress && taskTotal > 0)
        {
            return "in progress";
        }

        if (specStatus is null)
        {
            return "drafting";
        }

        if (specStatus == PlanningArtifactStatus.Draft)
        {
            return "writing spec";
        }

        if (specStatus == PlanningArtifactStatus.Validated && planStatus != PlanningArtifactStatus.Validated)
        {
            return "planning";
        }

        return "plan validated";
    }

    public static string? BriefExcerpt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var goal = PlanningSections.Find("goal", body)?.Content;
        if (string.IsNullOrWhiteSpace(goal))
        {
            return null;
        }

        var paragraph = FirstParagraph(goal);
        if (paragraph.Length == 0)
        {
            return null;
        }

        var info = new StringInfo(paragraph);
        return info.LengthInTextElements <= ExcerptCap
            ? paragraph
            : info.SubstringByTextElements(0, ExcerptCap) + Ellipsis;
    }

    private static string FirstParagraph(string goal)
    {
        var lines = goal
            .Split('\n')
            .Select(line => line.Trim().TrimEnd('\r'))
            .SkipWhile(line => line.Length == 0);

        var paragraph = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                break;
            }

            paragraph.Add(line);
        }

        return string.Join(' ', paragraph);
    }
}
