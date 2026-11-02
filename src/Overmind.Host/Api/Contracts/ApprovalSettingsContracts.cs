namespace Overmind.Host.Api.Contracts;

public sealed record UpdateApprovalSettingsRequest(bool DelegateApprovals);

public sealed record ApprovalSettingsResponse(bool DelegateApprovals);
