import type { AgentPlatform, EntryType } from "@/shared/api/types";

export type KitSourceKind = "manual" | "imported" | "bundled";

export interface KitResponse {
  id: string;
  name: string;
  version: number;
  description: string;
  entryIds: string[];
  sourceKind: KitSourceKind;
  sourcePath: string | null;
  importedAtUtc: string | null;
}

export interface KitFormValues {
  name: string;
  version: number;
  description: string;
}

export interface MaterializedFileResponse {
  platform: AgentPlatform;
  relativePath: string;
  sha256: string;
}

export interface MaterializationReportResponse {
  projectId: string;
  kitId: string;
  files: MaterializedFileResponse[];
}

export interface DriftItemResponse {
  platform: AgentPlatform;
  relativePath: string;
  kind: "missing" | "modified";
}

export type KitScanIssueCode =
  | "unreadableFile"
  | "emptyFile"
  | "invalidFrontmatter"
  | "invalidName"
  | "invalidDescription"
  | "invalidType"
  | "platformNativeField";

export interface KitScanSkillResponse {
  name: string;
  relativePath: string;
  platform: AgentPlatform | null;
  fileCount: number;
}

export interface KitScanRuleResponse {
  relativePath: string;
  title: string;
}

export interface KitScanIssueResponse {
  relativePath: string;
  code: KitScanIssueCode;
  message: string;
}

export interface KitScanCandidateResponse {
  name: string;
  path: string;
  relativePath: string;
  nameCollision: boolean;
  nameCollisionKitId: string | null;
  reImportMatchKitId: string | null;
  skills: KitScanSkillResponse[];
  gates: KitScanRuleResponse[];
  guards: KitScanRuleResponse[];
  issues: KitScanIssueResponse[];
}

export interface KitScanPreviewResponse {
  scannedPath: string;
  truncated: boolean;
  candidates: KitScanCandidateResponse[];
}

export interface KitImportSelectionRequest {
  relativePath: string;
  name: string;
}

export type KitImportFailureKind =
  | "pathMissing"
  | "notADirectory"
  | "duplicateName"
  | "reImportRequired"
  | "candidateNotFound"
  | "kitNotFound"
  | "noSource"
  | "sourceMissing";

export interface KitImportKitResultResponse {
  name: string;
  kitId: string | null;
  success: boolean;
  failureKind: KitImportFailureKind | null;
  error: string | null;
}

export interface KitImportReportResponse {
  scannedPath: string;
  kits: KitImportKitResultResponse[];
}

export type KitReimportOutcome = "added" | "updated" | "keptLocal" | "conflicted" | "removed";

export interface KitReimportEntryResponse {
  outcome: KitReimportOutcome;
  entryType: EntryType;
  title: string;
  relativePath: string | null;
  entryId: string | null;
  importedContentHash: string | null;
  entryRowRetained: boolean;
}

export interface KitReimportReportResponse {
  kitId: string;
  scannedPath: string;
  addedCount: number;
  updatedCount: number;
  keptLocalCount: number;
  conflictedCount: number;
  removedCount: number;
  entries: KitReimportEntryResponse[];
}
