namespace Overmind.Host.Api.Contracts;

public sealed record RegisterAgentSessionRequest(
    string Alias, string Vendor, string? Role = null, Guid? PriorSessionId = null);

public sealed record SetAgentSessionRoleRequest(string Role);

public sealed record SetAgentSessionIdentityRequest(string Alias, string Vendor);

public sealed record AgentSessionResponse(
    Guid Id,
    string Alias,
    string Vendor,
    DateTime StartedAt,
    DateTime LastSeenAt,
    DateTime? EndedAt);

public sealed record AgentSessionIdentityResponse(AgentSessionResponse Session, IReadOnlyList<string> Warnings);
