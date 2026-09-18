export type PlanningFeatureStatus = "draft" | "ready" | "inProgress" | "done";

export type PlanningArtifactKind = "spec" | "plan" | "task" | "result";

export type PlanningArtifactStatus =
  | "draft"
  | "validated"
  | "todo"
  | "inProgress"
  | "review"
  | "done"
  | "blocked";

export type PlanningReviewLevel = "light" | "hard";

export type PlanningMaterialization = "inherit" | "off" | "on" | "archive";

export type HumanValidationOverride = "unset" | "on" | "off";

export type PlanningDriftKind = "missing" | "modified";

export type PlanningTab = "spec" | "plan" | "tasks" | "result";

export type PlanningDocumentKind = Exclude<PlanningArtifactKind, "task">;

export const PLANNING_FEATURE_STATUSES: readonly PlanningFeatureStatus[] = [
  "draft",
  "ready",
  "inProgress",
  "done",
];

export const PLANNING_TABS: readonly PlanningTab[] = ["spec", "plan", "tasks", "result"];

export const TASK_STATUSES: readonly PlanningArtifactStatus[] = [
  "todo",
  "inProgress",
  "review",
  "done",
  "blocked",
];

export const PLANNING_MATERIALIZATIONS: readonly PlanningMaterialization[] = [
  "inherit",
  "off",
  "on",
  "archive",
];

export const HUMAN_VALIDATION_OVERRIDES: readonly HumanValidationOverride[] = [
  "unset",
  "on",
  "off",
];

export interface PlanningFeatureBoardItem {
  slug: string;
  title: string;
  status: PlanningFeatureStatus;
  isCurrent: boolean;
  version: number;
  specStatus: PlanningArtifactStatus | null;
  planStatus: PlanningArtifactStatus | null;
  taskTotal: number;
  taskDone: number;
  updatedAt: string;
}

export interface PlanningLogLine {
  date: string | null;
  text: string;
}

export interface PlanningArtifactResponse {
  id: string;
  kind: PlanningArtifactKind;
  status: PlanningArtifactStatus;
  review: PlanningReviewLevel;
  order: number;
  body: string;
  frozenAt: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
  log: PlanningLogLine[];
}

export interface PlanningFeatureResponse {
  slug: string;
  title: string;
  body: string;
  status: PlanningFeatureStatus;
  version: number;
  createdAt: string;
  updatedAt: string;
  artifacts: PlanningArtifactResponse[];
}

export interface PlanningArtifactVersionResponse {
  version: number;
  body: string;
  createdAt: string;
}

export interface PlanningWorkflowResponse {
  materialization: PlanningMaterialization;
  requireHumanValidation: HumanValidationOverride;
  effectiveMaterialization: PlanningMaterialization;
  effectiveRequireHumanValidation: boolean;
}

export interface PlanningCurrentFeature {
  slug: string;
  title: string;
  status: PlanningFeatureStatus;
}

export interface PlanningCurrentResponse {
  state: string;
  feature: PlanningCurrentFeature;
  artifact: PlanningArtifactResponse | null;
  reason: string | null;
}

export interface PlanningDriftItem {
  projectId: string;
  featureSlug: string | null;
  relativePath: string;
  kind: PlanningDriftKind;
}

export interface PlanningMaterializedFile {
  relativePath: string;
  sha256: string;
}

export interface PlanningMaterializationReport {
  projectId: string;
  strategy: PlanningMaterialization;
  files: PlanningMaterializedFile[];
  warnings: string[];
}

export interface PlanningFeatureFormValues {
  slug: string;
  title: string;
  body: string;
}

export interface UpdatePlanningFeatureValues {
  title?: string;
  body?: string;
  status?: PlanningFeatureStatus;
}

export interface CreatePlanningArtifactValues {
  kind: PlanningArtifactKind;
  body: string;
  order?: number;
  review?: PlanningReviewLevel;
  logNote?: string;
}

export interface UpdatePlanningArtifactValues {
  body: string;
  logNote?: string;
}

export interface UpdatePlanningWorkflowValues {
  materialization?: PlanningMaterialization;
  requireHumanValidation?: HumanValidationOverride;
}
