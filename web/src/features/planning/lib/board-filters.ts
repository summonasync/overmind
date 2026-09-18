import {
  PLANNING_FEATURE_STATUSES,
} from "@/features/planning/types";
import type {
  PlanningArtifactStatus,
  PlanningFeatureBoardItem,
  PlanningFeatureStatus,
} from "@/features/planning/types";

export type PlanningBoardArtifactFilter = "any" | "validated" | "notValidated" | "none";

export interface PlanningBoardFilters {
  statuses: PlanningFeatureStatus[];
  spec: PlanningBoardArtifactFilter;
  plan: PlanningBoardArtifactFilter;
  currentOnly: boolean;
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
  spec: "any",
  plan: "any",
  currentOnly: false,
};

const ARTIFACT_FILTER_VALUES = new Set<string>(BOARD_ARTIFACT_FILTERS);

export function parseBoardFilters(searchParams: BoardFilterSearchParams): PlanningBoardFilters {
  const statuses = (searchParams.get("status") ?? "")
    .split(",")
    .filter((value): value is PlanningFeatureStatus =>
      (PLANNING_FEATURE_STATUSES as readonly string[]).includes(value),
    );
  return {
    statuses,
    spec: parseArtifactFilter(searchParams.get("spec")),
    plan: parseArtifactFilter(searchParams.get("plan")),
    currentOnly: searchParams.get("current") === "1",
  };
}

export function isBoardFilterActive(filters: PlanningBoardFilters): boolean {
  return (
    filters.statuses.length > 0 ||
    filters.spec !== "any" ||
    filters.plan !== "any" ||
    filters.currentOnly
  );
}

export function filterBoardItems(
  items: PlanningFeatureBoardItem[],
  filters: PlanningBoardFilters,
): PlanningFeatureBoardItem[] {
  if (!isBoardFilterActive(filters)) {
    return items;
  }
  return items.filter(
    (item) =>
      (filters.statuses.length === 0 || filters.statuses.includes(item.status)) &&
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
