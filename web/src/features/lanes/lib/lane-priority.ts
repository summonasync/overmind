import { FEATURE_PRIORITIES } from "@/features/planning";
import type { PlanningFeaturePriority } from "@/features/planning";

export function lanePriorityToValue(priority: PlanningFeaturePriority): number {
  return FEATURE_PRIORITIES.indexOf(priority);
}

export function lanePriorityFromValue(value: number): PlanningFeaturePriority | undefined {
  return FEATURE_PRIORITIES[value];
}
