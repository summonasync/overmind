using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record UpdateApprovalSettingsRequest(string? DelegateApprovals);

public sealed record ApprovalSettingsResponse(
    DelegateApprovalsOverride DelegateApprovals,
    bool EffectiveDelegateApprovals);
