using Overmind.Core;
using Overmind.Infrastructure.Kits;

namespace Overmind.Host.Api.Contracts;

public sealed record KitScanRequest(string Path);

public sealed record KitScanSkillResponse(string Name, string RelativePath, AgentPlatform? Platform, int FileCount);

public sealed record KitScanRuleResponse(string RelativePath, string Title);

public sealed record KitScanIssueResponse(string RelativePath, KitScanIssueCode Code, string Message);

public sealed record KitScanCandidateResponse(
    string Name,
    string Path,
    string RelativePath,
    bool NameCollision,
    Guid? NameCollisionKitId,
    Guid? ReImportMatchKitId,
    IReadOnlyList<KitScanSkillResponse> Skills,
    IReadOnlyList<KitScanRuleResponse> Gates,
    IReadOnlyList<KitScanRuleResponse> Guards,
    IReadOnlyList<KitScanIssueResponse> Issues);

public sealed record KitScanPreviewResponse(
    string ScannedPath,
    bool Truncated,
    IReadOnlyList<KitScanCandidateResponse> Candidates);

public sealed record KitImportSelectionRequest(string RelativePath, string Name);

public sealed record KitImportRequest(string Path, IReadOnlyList<KitImportSelectionRequest> Kits);

public sealed record KitImportKitResultResponse(
    string Name,
    Guid? KitId,
    bool Success,
    KitImportFailureKind? FailureKind,
    string? Error);

public sealed record KitImportReportResponse(
    string ScannedPath,
    IReadOnlyList<KitImportKitResultResponse> Kits);

public sealed record KitReimportEntryResponse(
    KitReimportOutcome Outcome,
    EntryType EntryType,
    string Title,
    string? RelativePath,
    Guid? EntryId,
    string? ImportedContentHash,
    bool EntryRowRetained);

public sealed record KitReimportReportResponse(
    Guid KitId,
    string ScannedPath,
    int AddedCount,
    int UpdatedCount,
    int KeptLocalCount,
    int ConflictedCount,
    int RemovedCount,
    IReadOnlyList<KitReimportEntryResponse> Entries);
