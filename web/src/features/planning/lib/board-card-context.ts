import type { PlanningFeatureBoardItem } from "@/features/planning/types";

export function cardBriefLine(item: PlanningFeatureBoardItem): string | null {
  const excerpt = item.briefExcerpt?.trim();
  return excerpt !== undefined && excerpt.length > 0 ? excerpt : null;
}

export function cardPhaseLabel(item: PlanningFeatureBoardItem): string {
  return item.phase;
}
