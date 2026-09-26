using Overmind.Core;

namespace Overmind.Host.Api;

public static class PlanningDoorSource
{
    public static PlanningLogSource Resolve(string? stated, PlanningLogSource fallback) =>
        stated?.Trim().ToLowerInvariant() switch
        {
            "agent" => PlanningLogSource.Agent,
            "ui" => PlanningLogSource.Ui,
            _ => fallback
        };
}
