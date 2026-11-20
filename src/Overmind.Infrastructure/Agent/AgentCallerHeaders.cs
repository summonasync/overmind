namespace Overmind.Infrastructure.Agent;

public static class AgentCallerHeaders
{
    public const string ScopeHeader = "X-Overmind-Caller-Project";
    public const string MarkerHeader = "X-Overmind-Agent-Call";
    public const string MarkerValue = "embedded-agent";
    public const string GlobalScopeValue = "global";

    public const string MarkerWithoutScopeCode = "agentCallerScopeMissing";
    public const string MarkerWithoutScopeMessage =
        "the agent marker '{0}' was sent without the caller scope header '{1}', so the host cannot read this as an " +
        "agent call at all. Nothing was sent. A conversation without a project still carries a scope — written as " +
        "'{2}' — so ask the transport for the pair instead of assembling these two headers by hand.";

    public const string ReservedHeaderCollisionCode = "agentCallerHeaderCollision";

    public const string ReservedHeaderCollisionMessage =
        "a handler passed '{0}' as a plain header while the transport was also given the caller, so two sources of " +
        "truth claim the same header. Nothing was sent. The caller identity belongs to the transport: give the " +
        "AgentCaller to the client overload and keep only your own headers in the dictionary.";

    public const string ReservedHeaderWithoutCallerMessage =
        "a handler handed '{0}' over as a plain header with no AgentCaller at all, so this call claims an identity " +
        "the transport never issued. Nothing was sent. The caller pair has exactly one way onto the wire: give the " +
        "AgentCaller to the client overload — it writes both headers, including the 'global' scope for a " +
        "conversation without a project.";

    public static IReadOnlyDictionary<string, string> ForCaller(AgentCaller caller) =>
        new Dictionary<string, string>
        {
            [MarkerHeader] = MarkerValue,
            [ScopeHeader] = caller.ScopeValue,
        };

    public static IReadOnlyDictionary<string, string> Pair(
        AgentCaller? caller,
        IReadOnlyDictionary<string, string>? additional)
    {
        if (caller is null)
        {
            var handed = additional ?? Empty;
            // Header names are case-insensitive on the wire, so the refusal has to be too: an ordinal
            // ContainsKey let a lowercase marker through without a scope (review-246-01 M1).
            var claimed = ClaimedReserved(handed);
            var markerClaimed = claimed.Contains(MarkerHeader);
            var scopeClaimed = claimed.Contains(ScopeHeader);

            if (markerClaimed && !scopeClaimed)
            {
                throw new AgentCallerContractException(
                    MarkerWithoutScopeCode,
                    string.Format(MarkerWithoutScopeMessage, MarkerHeader, ScopeHeader, GlobalScopeValue));
            }

            // A full pair assembled by hand is still identity the transport never issued — one way onto the
            // wire is the AgentCaller parameter (review-246-01 S1).
            if (claimed.Count > 0)
            {
                throw new AgentCallerContractException(
                    ReservedHeaderCollisionCode,
                    string.Format(ReservedHeaderWithoutCallerMessage, string.Join(", ", claimed)));
            }

            return handed;
        }

        if (additional is not null)
        {
            var claimed = ClaimedReserved(additional);
            if (claimed.Count > 0)
            {
                throw new AgentCallerContractException(
                    ReservedHeaderCollisionCode,
                    string.Format(ReservedHeaderCollisionMessage, string.Join(", ", claimed)));
            }
        }

        if (additional is null)
        {
            return ForCaller(caller);
        }

        var merged = new Dictionary<string, string>(ForCaller(caller));
        foreach (var (name, value) in additional)
        {
            merged[name] = value;
        }

        return merged;
    }

    public static bool IsReserved(string name) =>
        name.Equals(ScopeHeader, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(MarkerHeader, StringComparison.OrdinalIgnoreCase);

    // The refusal names the headers canonically rather than as typed — these messages reach the model, and
    // echoing a caller's own casing back at it is an echo all the same.
    private static IReadOnlyList<string> ClaimedReserved(IReadOnlyDictionary<string, string> headers)
    {
        var claimed = new List<string>(2);
        if (headers.Keys.Any(name => name.Equals(MarkerHeader, StringComparison.OrdinalIgnoreCase)))
        {
            claimed.Add(MarkerHeader);
        }

        if (headers.Keys.Any(name => name.Equals(ScopeHeader, StringComparison.OrdinalIgnoreCase)))
        {
            claimed.Add(ScopeHeader);
        }

        return claimed;
    }

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
}
