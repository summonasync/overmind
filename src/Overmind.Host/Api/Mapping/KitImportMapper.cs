using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Kits;

namespace Overmind.Host.Api.Mapping;

public static class KitImportMapper
{
    public static KitScanPreviewResponse ToResponse(KitScanResult scan) =>
        new(scan.ScannedPath, scan.Truncated, scan.Candidates.Select(ToResponse).ToArray());

    public static KitScanCandidateResponse ToResponse(KitScanCandidate candidate) =>
        new(
            candidate.Name,
            candidate.Path,
            candidate.RelativePath,
            candidate.NameCollision,
            candidate.NameCollisionKitId,
            candidate.ReImportMatchKitId,
            candidate.Skills.Select(s => new KitScanSkillResponse(s.Name, s.RelativePath, s.Platform, s.FileCount)).ToArray(),
            candidate.Gates.Select(ToResponse).ToArray(),
            candidate.Guards.Select(ToResponse).ToArray(),
            candidate.Issues.Select(i => new KitScanIssueResponse(i.RelativePath, i.Code, i.Message)).ToArray());

    public static KitImportReportResponse ToResponse(KitImportReport report) =>
        new(report.ScannedPath, report.Kits.Select(ToResponse).ToArray());

    public static KitImportKitResultResponse ToResponse(KitImportKitResult kit) =>
        new(kit.Name, kit.KitId, kit.Success, kit.FailureKind, kit.Error);

    public static KitReimportReportResponse ToResponse(KitReimportReport report) =>
        new(
            report.KitId,
            report.ScannedPath,
            report.AddedCount,
            report.UpdatedCount,
            report.KeptLocalCount,
            report.ConflictedCount,
            report.RemovedCount,
            report.Entries.Select(ToResponse).ToArray());

    public static KitReimportEntryResponse ToResponse(KitReimportEntryResult entry) =>
        new(
            entry.Outcome,
            entry.EntryType,
            entry.Title,
            entry.RelativePath,
            entry.EntryId,
            entry.ImportedContentHash,
            entry.EntryRowRetained);

    private static KitScanRuleResponse ToResponse(KitScanGate rule) =>
        new(rule.RelativePath, rule.Title);
}
