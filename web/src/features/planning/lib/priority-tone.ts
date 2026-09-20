import type { BadgeTone } from "@/shared/ui/badge";
import type { PlanningFeaturePriority } from "@/features/planning/types";

const PRIORITY_TONES: Record<PlanningFeaturePriority, BadgeTone> = {
  p0: "danger",
  p1: "warning",
  p2: "neutral",
  p3: "muted",
};

export function priorityTone(priority: PlanningFeaturePriority): BadgeTone {
  return PRIORITY_TONES[priority];
}
