import type { IssueStatus } from "@/features/issues/types";

type StatusTone = "muted" | "info" | "accent" | "success";

const STATUS_TONES: Record<IssueStatus, StatusTone> = {
  open: "muted",
  triaged: "info",
  promoted: "accent",
  closed: "success",
};

const TONE_CLASSES: Record<StatusTone, string> = {
  muted: "border-border bg-surface-raised text-muted",
  info: "border-primary/30 bg-primary/10 text-primary",
  accent: "border-warning/40 bg-warning/10 text-warning",
  success: "border-success/30 bg-success/10 text-success",
};

export function issueStatusTone(status: IssueStatus): string {
  return TONE_CLASSES[STATUS_TONES[status]];
}
