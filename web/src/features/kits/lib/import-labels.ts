import type {
  BadgeTone,
} from "@/shared/ui/badge";
import { parseInstant } from "@/shared/lib/relative-time";
import type {
  KitImportFailureKind,
  KitReimportOutcome,
  KitScanIssueCode,
} from "@/features/kits/types";

export const IMPORT_FAILURE_LABELS: Record<KitImportFailureKind, string> = {
  pathMissing: "Path missing",
  notADirectory: "Not a directory",
  duplicateName: "Duplicate name",
  reImportRequired: "Re-import required",
  candidateNotFound: "Candidate not found",
  kitNotFound: "Kit not found",
  noSource: "No source",
  sourceMissing: "Source missing",
};

export const SCAN_ISSUE_LABELS: Record<KitScanIssueCode, string> = {
  unreadableFile: "Unreadable file",
  emptyFile: "Empty file",
  invalidFrontmatter: "Invalid frontmatter",
  invalidName: "Invalid name",
  invalidDescription: "Invalid description",
  invalidType: "Invalid type",
  platformNativeField: "Platform-native field",
};

export const REIMPORT_OUTCOME_LABELS: Record<KitReimportOutcome, string> = {
  added: "Added",
  updated: "Updated",
  keptLocal: "Kept local",
  conflicted: "Conflicted",
  removed: "Removed",
};

export const REIMPORT_OUTCOME_TONES: Record<KitReimportOutcome, BadgeTone> = {
  added: "success",
  updated: "info",
  keptLocal: "neutral",
  conflicted: "danger",
  removed: "warning",
};

export function formatImportedAt(iso: string): string {
  const timestamp = parseInstant(iso);
  return timestamp === null ? iso : new Date(timestamp).toLocaleString();
}
