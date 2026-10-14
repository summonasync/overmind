import {
  FEATURE_PRIORITIES,
  PLANNING_FEATURE_STATUSES,
} from "../types.ts";
import type {
  PlanningArtifactStatus,
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureStatus,
} from "../types.ts";

export type PlanningBoardArtifactFilter = "any" | "validated" | "notValidated" | "none";

export interface PlanningBoardFilters {
  statuses: PlanningFeatureStatus[];
  priorities: PlanningFeaturePriority[];
  spec: PlanningBoardArtifactFilter;
  plan: PlanningBoardArtifactFilter;
  pinnedOnly: boolean;
  hideDone: boolean;
  blockedOnly: boolean;
  attentionOnly: boolean;
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
  pinnedOnly: false,
  hideDone: true,
  blockedOnly: false,
  attentionOnly: false,
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
    pinnedOnly: searchParams.get("pinned") === "1",
    hideDone: searchParams.get("hidedone") !== "0",
    blockedOnly: searchParams.get("blocked") === "1",
    attentionOnly: searchParams.get("attention") === "1",
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
    pinnedOnly: record.pinnedOnly === true,
    hideDone: record.hideDone !== false,
    blockedOnly: record.blockedOnly === true,
    attentionOnly: record.attentionOnly === true,
  };
}

export function isBoardFilterActive(filters: PlanningBoardFilters): boolean {
  return (
    filters.statuses.length > 0 ||
    filters.priorities.length > 0 ||
    filters.spec !== "any" ||
    filters.plan !== "any" ||
    filters.pinnedOnly ||
    !filters.hideDone ||
    filters.blockedOnly ||
    filters.attentionOnly
  );
}

export type BoardFastChipId = "current" | "hideDone" | "blocked";

export interface BoardAttentionContext {
  awaitingFeatureSlugs: ReadonlySet<string>;
  humanValidationOn: boolean;
}

const EMPTY_ATTENTION_CONTEXT: BoardAttentionContext = {
  awaitingFeatureSlugs: new Set<string>(),
  humanValidationOn: false,
};

export function isAttentionItem(
  item: PlanningFeatureBoardItem,
  context: BoardAttentionContext,
): boolean {
  if (item.status === "review") {
    return true;
  }
  if (context.awaitingFeatureSlugs.has(item.slug)) {
    return true;
  }
  return (
    context.humanValidationOn &&
    (item.specStatus === "draft" || item.planStatus === "draft")
  );
}

export function toggleBoardStatusFilter(
  filters: PlanningBoardFilters,
  status: PlanningFeatureStatus,
): PlanningBoardFilters {
  const statuses = filters.statuses.includes(status)
    ? filters.statuses.filter((item) => item !== status)
    : [...filters.statuses, status];
  return { ...filters, statuses };
}

export function toggleBoardPriorityFilter(
  filters: PlanningBoardFilters,
  priority: PlanningFeaturePriority,
): PlanningBoardFilters {
  const priorities = filters.priorities.includes(priority)
    ? filters.priorities.filter((item) => item !== priority)
    : [...filters.priorities, priority];
  return { ...filters, priorities };
}

export function toggleBoardFastChip(
  filters: PlanningBoardFilters,
  chip: BoardFastChipId,
): PlanningBoardFilters {
  switch (chip) {
    case "current":
      return { ...filters, pinnedOnly: !filters.pinnedOnly };
    case "hideDone":
      return { ...filters, hideDone: !filters.hideDone };
    case "blocked":
      return { ...filters, blockedOnly: !filters.blockedOnly };
  }
}

export function writeBoardFilterParams(
  params: URLSearchParams,
  filters: PlanningBoardFilters,
): void {
  if (filters.statuses.length > 0) {
    params.set("status", filters.statuses.join(","));
  } else {
    params.delete("status");
  }
  if (filters.priorities.length > 0) {
    params.set("priority", filters.priorities.join(","));
  } else {
    params.delete("priority");
  }
  if (filters.spec !== "any") {
    params.set("spec", filters.spec);
  } else {
    params.delete("spec");
  }
  if (filters.plan !== "any") {
    params.set("plan", filters.plan);
  } else {
    params.delete("plan");
  }
  if (filters.pinnedOnly) {
    params.set("pinned", "1");
  } else {
    params.delete("pinned");
  }
  if (!filters.hideDone) {
    params.set("hidedone", "0");
  } else {
    params.delete("hidedone");
  }
  if (filters.blockedOnly) {
    params.set("blocked", "1");
  } else {
    params.delete("blocked");
  }
  if (filters.attentionOnly) {
    params.set("attention", "1");
  } else {
    params.delete("attention");
  }
}

export function filterBoardItems(
  items: PlanningFeatureBoardItem[],
  filters: PlanningBoardFilters,
  attention: BoardAttentionContext = EMPTY_ATTENTION_CONTEXT,
): PlanningFeatureBoardItem[] {
  const hideDone = filters.statuses.length === 0 && filters.hideDone;
  return items.filter(
    (item) =>
      (!hideDone || item.status !== "done") &&
      (filters.statuses.length === 0 || filters.statuses.includes(item.status)) &&
      (filters.priorities.length === 0 || filters.priorities.includes(item.priority)) &&
      matchesArtifactFilter(item.specStatus, filters.spec) &&
      matchesArtifactFilter(item.planStatus, filters.plan) &&
      (!filters.pinnedOnly || item.claimedByAlias !== null) &&
      (!filters.blockedOnly || item.isBlocked) &&
      (!filters.attentionOnly || isAttentionItem(item, attention)),
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
