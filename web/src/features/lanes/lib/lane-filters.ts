import { PLANNING_FEATURE_STATUSES } from "@/features/planning";
import type { PlanningFeatureStatus } from "@/features/planning";

export const LANE_FILTER_STATUSES: readonly PlanningFeatureStatus[] =
  PLANNING_FEATURE_STATUSES.filter((status) => status !== "done");

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
