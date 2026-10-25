namespace Overmind.Host.Api.Contracts;

public sealed record RegisterAgentSessionRequest(string Alias, string Vendor, string? Role = null);

public sealed record SetAgentSessionRoleRequest(string Role);

public sealed record AgentSessionResponse(
    Guid Id,
    string Alias,
    string Vendor,
    DateTime StartedAt,
    DateTime LastSeenAt,
    DateTime? EndedAt);
