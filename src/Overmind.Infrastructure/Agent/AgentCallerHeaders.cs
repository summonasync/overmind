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
            if (handed.Keys.Any(k => k.Equals(MarkerHeader, StringComparison.OrdinalIgnoreCase)) &&
                !handed.Keys.Any(k => k.Equals(ScopeHeader, StringComparison.OrdinalIgnoreCase)))
            {
                throw new AgentCallerContractException(
                    MarkerWithoutScopeCode,
                    string.Format(MarkerWithoutScopeMessage, MarkerHeader, ScopeHeader, GlobalScopeValue));
            }

            return handed;
        }

        if (additional is not null)
        {
            var claimed = additional.Keys
                .Where(IsReserved)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
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

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
}
