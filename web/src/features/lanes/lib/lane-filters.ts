import type { PlanningFeatureStatus } from "@/features/planning";

export function isStatusVisible(
  selected: ReadonlySet<PlanningFeatureStatus>,
  status: PlanningFeatureStatus,
): boolean {
  return selected.size === 0 || selected.has(status);
}

export function toggleStatus(
  selected: ReadonlySet<PlanningFeatureStatus>,
  status: PlanningFeatureStatus,
): ReadonlySet<PlanningFeatureStatus> {
  const next = new Set(selected);
  if (next.has(status)) {
    next.delete(status);
  } else {
    next.add(status);
  }
  return next;
}
