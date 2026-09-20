import { PLANNING_FEATURE_STATUSES, type PlanningFeatureStatus } from "@/features/planning/types";

const FEATURE_TRANSITIONS: Record<PlanningFeatureStatus, readonly PlanningFeatureStatus[]> = {
  draft: ["ready", "inProgress", "done"],
  ready: ["inProgress", "draft", "done"],
  inProgress: ["review", "ready", "draft", "done"],
  review: ["done", "inProgress"],
  done: [],
};

export function allowedTransitions(status: PlanningFeatureStatus): readonly PlanningFeatureStatus[] {
  return FEATURE_TRANSITIONS[status];
}

export function featureStatusOptions(status: PlanningFeatureStatus): readonly PlanningFeatureStatus[] {
  const choices = new Set<PlanningFeatureStatus>(allowedTransitions(status));
  choices.add(status);
  return PLANNING_FEATURE_STATUSES.filter((candidate) => choices.has(candidate));
}
