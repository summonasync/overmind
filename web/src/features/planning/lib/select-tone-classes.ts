import type { BadgeTone } from "@/shared/ui/badge";

export const selectToneClasses: Record<BadgeTone, string> = {
  neutral: "border-border text-muted",
  muted: "border-border/50 text-muted/70",
  info: "border-info/40 text-info",
  warning: "border-warning/40 text-warning",
  success: "border-success/40 text-success",
  danger: "border-danger/40 text-danger",
  skill: "border-type-skill/40 text-type-skill",
  idea: "border-type-featureRequest/40 text-type-featureRequest",
  audit: "border-type-audit/40 text-type-audit",
};
