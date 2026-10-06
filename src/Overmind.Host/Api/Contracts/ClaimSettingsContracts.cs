namespace Overmind.Host.Api.Contracts;

public sealed record UpdateClaimSettingsRequest(
    int ClaimStaleThresholdMinutes,
    string? AgentNamingRules);

public sealed record ClaimSettingsResponse(
    int ClaimStaleThresholdMinutes,
    string? AgentNamingRules);
