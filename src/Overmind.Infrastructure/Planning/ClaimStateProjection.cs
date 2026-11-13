using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public static class ClaimStateProjection
{
    public static AgentSessionLiveness ToClaimState(this AgentSessionLiveness liveness) =>
        liveness == AgentSessionLiveness.Ended ? AgentSessionLiveness.Released : liveness;
}
