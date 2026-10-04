import type { PlanningFeatureDependencyEntry } from "@/features/planning/types";

export const WAITS_FOR_HEADING = "Waits for";
export const CLEARED_BY_HEADING = "Cleared by";

export interface BlockerGroups {
  waiting: PlanningFeatureDependencyEntry[];
  cleared: PlanningFeatureDependencyEntry[];
}

export function splitBlockersByClosure(entries: PlanningFeatureDependencyEntry[]): BlockerGroups {
  return {
    waiting: entries.filter((entry) => !entry.closed),
    cleared: entries.filter((entry) => entry.closed),
  };
}

export function blockerChipClasses(closed: boolean): string {
  return closed ? "border-border/50 text-muted/70" : "border-danger/40 bg-danger/10 text-danger";
}

export function waitsForPlaceholder(totalCount: number, openCount: number): string | null {
  if (totalCount === 0) {
    return "nothing yet";
  }
  return openCount === 0 ? "nothing open" : null;
}
