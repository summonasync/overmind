import type { BadgeTone } from "@/shared/ui/badge";
import type { PlanningArtifactStatus, PlanningFeatureStatus } from "@/features/planning/types";

const FEATURE_TONES: Record<PlanningFeatureStatus, BadgeTone> = {
  draft: "neutral",
  ready: "info",
  inProgress: "warning",
  done: "success",
};

const ARTIFACT_TONES: Record<PlanningArtifactStatus, BadgeTone> = {
  draft: "neutral",
  validated: "success",
  todo: "neutral",
  inProgress: "warning",
  review: "info",
  done: "success",
  blocked: "danger",
};

export function featureStatusTone(status: PlanningFeatureStatus): BadgeTone {
  return FEATURE_TONES[status];
}

export function artifactStatusTone(status: PlanningArtifactStatus): BadgeTone {
  return ARTIFACT_TONES[status];
}
