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
        int taskTotal,
        int taskDone,
        PlanningArtifactStatus? resultStatus)
    {
        if (status == PlanningFeatureStatus.Done)
        {
            return "done";
        }

        var awaitingHuman = IsAwaitingHuman(status, taskTotal, taskDone, resultStatus);

        if (status == PlanningFeatureStatus.Review && !awaitingHuman)
        {
            return "review";
        }

        if (status == PlanningFeatureStatus.InProgress && taskTotal > 0 && !awaitingHuman)
        {
            return "in progress";
        }

        if (awaitingHuman)
        {
            return "awaiting-human";
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

    private static bool IsAwaitingHuman(
        PlanningFeatureStatus status,
        int taskTotal,
        int taskDone,
        PlanningArtifactStatus? resultStatus) =>
        resultStatus == PlanningArtifactStatus.Validated &&
        (status == PlanningFeatureStatus.Review ||
         (status == PlanningFeatureStatus.InProgress && taskTotal > 0 && taskDone == taskTotal));

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
