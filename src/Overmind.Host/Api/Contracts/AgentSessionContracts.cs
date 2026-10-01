namespace Overmind.Host.Api.Contracts;

public sealed record RegisterAgentSessionRequest(string Alias, string Vendor);

public sealed record AgentSessionResponse(
    Guid Id,
    string Alias,
    string Vendor,
    DateTime StartedAt,
    DateTime LastSeenAt,
    DateTime? EndedAt);
