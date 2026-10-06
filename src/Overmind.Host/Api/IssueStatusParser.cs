using Overmind.Core;

namespace Overmind.Host.Api;

internal static class IssueStatusParser
{
    internal static (IssueStatus? Parsed, string? Error) Parse(string? status)
    {
        if (status is null)
        {
            return (null, null);
        }

        if (!PlanningFeaturesApi.TryParseEnum<IssueStatus>(status, out var parsed))
        {
            return (null,
                $"Unknown status '{status}'. Allowed values: open, triaged, promoted, closed.");
        }

        return (parsed, null);
    }
}
