import {
  FEATURE_PRIORITIES,
  PLANNING_FEATURE_STATUSES,
} from "@/features/planning/types";
import type {
  PlanningArtifactStatus,
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureStatus,
} from "@/features/planning/types";

export type PlanningBoardArtifactFilter = "any" | "validated" | "notValidated" | "none";

export interface PlanningBoardFilters {
  statuses: PlanningFeatureStatus[];
  priorities: PlanningFeaturePriority[];
  spec: PlanningBoardArtifactFilter;
  plan: PlanningBoardArtifactFilter;
  currentOnly: boolean;
  hideDone: boolean;
}

export interface BoardFilterSearchParams {
  get(name: string): string | null;
}

export const BOARD_ARTIFACT_FILTERS: readonly PlanningBoardArtifactFilter[] = [
  "any",
  "validated",
  "notValidated",
  "none",
];

export const EMPTY_BOARD_FILTERS: PlanningBoardFilters = {
  statuses: [],
  priorities: [],
  spec: "any",
  plan: "any",
  currentOnly: false,
  hideDone: true,
};

const ARTIFACT_FILTER_VALUES = new Set<string>(BOARD_ARTIFACT_FILTERS);

export function parseBoardFilters(searchParams: BoardFilterSearchParams): PlanningBoardFilters {
  const statuses = (searchParams.get("status") ?? "")
    .split(",")
    .filter((value): value is PlanningFeatureStatus =>
      (PLANNING_FEATURE_STATUSES as readonly string[]).includes(value),
    );
  const priorities = (searchParams.get("priority") ?? "")
    .split(",")
    .filter((value): value is PlanningFeaturePriority =>
      (FEATURE_PRIORITIES as readonly string[]).includes(value),
    );
  return {
    statuses,
    priorities,
    spec: parseArtifactFilter(searchParams.get("spec")),
    plan: parseArtifactFilter(searchParams.get("plan")),
    currentOnly: searchParams.get("current") === "1",
    hideDone: searchParams.get("hidedone") !== "0",
  };
}

export function coerceBoardFilters(value: unknown): PlanningBoardFilters | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }
  const record = value as Record<string, unknown>;
  return {
    statuses: Array.isArray(record.statuses)
      ? record.statuses.filter((status): status is PlanningFeatureStatus =>
          (PLANNING_FEATURE_STATUSES as readonly string[]).includes(status as string),
        )
      : [],
    priorities: Array.isArray(record.priorities)
      ? record.priorities.filter((priority): priority is PlanningFeaturePriority =>
          (FEATURE_PRIORITIES as readonly string[]).includes(priority as string),
        )
      : [],
    spec: parseArtifactFilter(typeof record.spec === "string" ? record.spec : null),
    plan: parseArtifactFilter(typeof record.plan === "string" ? record.plan : null),
    currentOnly: record.currentOnly === true,
    hideDone: record.hideDone !== false,
  };
}

export function isBoardFilterActive(filters: PlanningBoardFilters): boolean {
  return (
    filters.statuses.length > 0 ||
    filters.priorities.length > 0 ||
    filters.spec !== "any" ||
    filters.plan !== "any" ||
    filters.currentOnly ||
    !filters.hideDone
  );
}

export function filterBoardItems(
  items: PlanningFeatureBoardItem[],
  filters: PlanningBoardFilters,
): PlanningFeatureBoardItem[] {
  const hideDone = filters.statuses.length === 0 && filters.hideDone;
  return items.filter(
    (item) =>
      (!hideDone || item.status !== "done") &&
      (filters.statuses.length === 0 || filters.statuses.includes(item.status)) &&
      (filters.priorities.length === 0 || filters.priorities.includes(item.priority)) &&
      matchesArtifactFilter(item.specStatus, filters.spec) &&
      matchesArtifactFilter(item.planStatus, filters.plan) &&
      (!filters.currentOnly || item.isCurrent),
  );
}

function parseArtifactFilter(value: string | null): PlanningBoardArtifactFilter {
  return value !== null && ARTIFACT_FILTER_VALUES.has(value)
    ? (value as PlanningBoardArtifactFilter)
    : "any";
}

function matchesArtifactFilter(
  status: PlanningArtifactStatus | null,
  filter: PlanningBoardArtifactFilter,
): boolean {
  switch (filter) {
    case "any":
      return true;
    case "none":
      return status === null;
    case "validated":
      return status === "validated";
    case "notValidated":
      return status !== null && status !== "validated";
  }
}
