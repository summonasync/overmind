using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record LaneHolder(
    string Alias,
    string Vendor,
    AgentSessionLiveness ClaimState,
    string Role = AgentSession.DefaultRole);
