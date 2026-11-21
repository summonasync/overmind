export type PlanningFeatureStatus = "draft" | "ready" | "inProgress" | "review" | "done";

export type PlanningFeaturePriority = "p0" | "p1" | "p2" | "p3";

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
  "review",
  "done",
];

export const FEATURE_PRIORITIES: readonly PlanningFeaturePriority[] = ["p0", "p1", "p2", "p3"];

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

export type PlanningClaimState = "alive" | "stale" | "ended" | "released";

export interface PlanningLaneHolder {
  alias: string;
  vendor: string;
  claimState: PlanningClaimState;
}

export interface PlanningFeatureBoardItem {
  slug: string;
  title: string;
  briefExcerpt: string | null;
  status: PlanningFeatureStatus;
  phase: string | null;
  priority: PlanningFeaturePriority;
  queuePosition: number;
  isBlocked: boolean;
  version: number;
  specStatus: PlanningArtifactStatus | null;
  planStatus: PlanningArtifactStatus | null;
  taskTotal: number;
  taskDone: number;
  taskCounts?: { todo: number; inProgress: number; review: number; done: number; blocked: number };
  updatedAt: string;
  claimedByAlias: string | null;
  claimedByVendor: string | null;
  claimedAt: string | null;
  claimState: PlanningClaimState | null;
  claimedByHuman: boolean;
  lane?: string | null;
  laneHolder?: PlanningLaneHolder | null;
}

export interface ReorderPlanningQueueValues {
  priority: PlanningFeaturePriority;
  slugs: string[];
}

export interface PlanningInboxItem {
  slug: string;
  title: string;
  phase: string;
  queuePosition: number;
  claimedByAlias: string | null;
  claimState: PlanningClaimState | null;
  pendingSince: string;
  link: string | null;
}

export interface PlanningInboxResponse {
  projectId: string;
  items: PlanningInboxItem[];
}

export interface PlanningFeatureDependencyEntry {
  slug: string;
  status: PlanningFeatureStatus;
  closed: boolean;
}

export interface PlanningFeatureDependenciesResponse {
  slug: string;
  blockedBy: PlanningFeatureDependencyEntry[];
  blocks: PlanningFeatureDependencyEntry[];
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
  brief?: string | null;
  frozenAt: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
  log: PlanningLogLine[];
  blocked?: boolean;
  blockedBy?: number[];
}

export interface PlanningFeatureResponse {
  slug: string;
  title: string;
  body: string;
  status: PlanningFeatureStatus;
  priority: PlanningFeaturePriority;
  version: number;
  createdAt: string;
  updatedAt: string;
  artifacts: PlanningArtifactResponse[];
  isBlocked: boolean;
  blockedBy: PlanningFeatureDependencyEntry[];
  blocks: PlanningFeatureDependencyEntry[];
  claimedByAlias: string | null;
  claimedByVendor: string | null;
  claimedAt: string | null;
  claimState: PlanningClaimState | null;
  claimedByHuman: boolean;
}

export interface PlanningFeatureClaimResponse {
  slug: string;
  claimedByAlias: string | null;
  claimedByVendor: string | null;
  claimedAt: string | null;
  claimState: PlanningClaimState | null;
  claimedByHuman: boolean;
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
  priority: PlanningFeaturePriority;
}

export interface UpdatePlanningFeatureValues {
  title?: string;
  body?: string;
  status?: PlanningFeatureStatus;
  priority?: PlanningFeaturePriority;
  lane?: string;
}

export interface CreatePlanningArtifactValues {
  kind: PlanningArtifactKind;
  body: string;
  order?: number;
  review?: PlanningReviewLevel;
  logNote?: string;
  brief?: string;
}

export interface UpdatePlanningArtifactValues {
  body: string;
  logNote?: string;
  brief?: string;
}

export interface UpdatePlanningWorkflowValues {
  materialization?: PlanningMaterialization;
  requireHumanValidation?: HumanValidationOverride;
}

export interface PlanningImportScanEntry {
  folder: string;
  slug: string | null;
  title: string | null;
  status: PlanningFeatureStatus | null;
  specs: number;
  plans: number;
  results: number;
  tasks: number;
  alreadyImported: boolean;
  error: string | null;
}

export interface PlanningImportScanReport {
  root: string;
  entries: PlanningImportScanEntry[];
  manifestHint?: string | null;
}

export interface PlanningFeatureImportReport {
  slug: string;
  title: string;
  status: PlanningFeatureStatus;
  created: boolean;
  artifactsCreated: number;
  artifactsUpdated: number;
  notes: string[];
  trackedFiles: number;
  materialization: PlanningMaterialization;
  nextStep: string | null;
}
