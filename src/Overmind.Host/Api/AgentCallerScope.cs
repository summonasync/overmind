using Overmind.Core;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Api;

// Reads the caller pair the 246 transport sends: X-Overmind-Agent-Call plus X-Overmind-Caller-Project.
// The external MCP adapter addresses boards through its own session project and sends neither header,
// and so does the human UI — an absent pair therefore means "not an embedded-agent call" and is not
// compared. A pair that arrives half-formed or unreadable is refused: the host would rather stop than
// guess which board a call belongs to.
public static class AgentCallerScope
{
    public static bool TryReadProjectId(
        HttpRequest http, out Guid? callerProjectId, out IResult? refusal)
    {
        callerProjectId = null;
        refusal = null;

        var marker = http.Headers.TryGetValue(AgentCallerHeaders.MarkerHeader, out var markers) &&
                     markers.Count > 0;
        var scope = http.Headers.TryGetValue(AgentCallerHeaders.ScopeHeader, out var scopes) &&
                    scopes.Count > 0;

        if (!marker && !scope)
        {
            return true;
        }

        if (marker ^ scope)
        {
            refusal = PlanningErrors.Map(PlanningException.AgentCallerScopeUnverifiable(
                marker
                    ? $"'{AgentCallerHeaders.MarkerHeader}' arrived without '{AgentCallerHeaders.ScopeHeader}'"
                    : $"'{AgentCallerHeaders.ScopeHeader}' arrived without '{AgentCallerHeaders.MarkerHeader}'"));
            return false;
        }

        var claimed = scopes[^1].Trim();
        if (claimed.Equals(AgentCallerHeaders.GlobalScopeValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Guid.TryParse(claimed, out var project))
        {
            callerProjectId = project;
            return true;
        }

        refusal = PlanningErrors.Map(PlanningException.AgentCallerScopeUnverifiable(
            $"'{AgentCallerHeaders.ScopeHeader}' carries a value that is neither a project id nor the " +
            $"literal '{AgentCallerHeaders.GlobalScopeValue}'"));
        return false;
    }
}
