import { FEATURE_PRIORITIES } from "@/features/planning";
import type { PlanningFeatureBoardItem, PlanningFeaturePriority } from "@/features/planning";
import { featureNumber } from "@/shared/lib/feature-number";
import type { LaneFeatureSummary } from "@/features/lanes/types";

function priorityIndex(priority: PlanningFeaturePriority | undefined): number {
  return priority === undefined ? FEATURE_PRIORITIES.length : FEATURE_PRIORITIES.indexOf(priority);
}

function featureOrderNumber(slug: string): number {
  const number = featureNumber(slug);
  if (number === null) {
    return Number.POSITIVE_INFINITY;
  }
  const parsed = Number.parseInt(number, 10);
  return Number.isNaN(parsed) ? Number.POSITIVE_INFINITY : parsed;
}

export function priorityBySlug(
  board: PlanningFeatureBoardItem[],
): ReadonlyMap<string, PlanningFeaturePriority> {
  return new Map(board.map((item) => [item.slug, item.priority] as const));
}

export function sortLaneFeatures(
  features: LaneFeatureSummary[],
  priorities: ReadonlyMap<string, PlanningFeaturePriority>,
): LaneFeatureSummary[] {
  return [...features].sort(
    (a, b) =>
      priorityIndex(priorities.get(a.slug)) - priorityIndex(priorities.get(b.slug)) ||
      featureOrderNumber(a.slug) - featureOrderNumber(b.slug) ||
      a.slug.localeCompare(b.slug),
  );
}

export function sortBoardItems(items: PlanningFeatureBoardItem[]): PlanningFeatureBoardItem[] {
  return [...items].sort(
    (a, b) =>
      priorityIndex(a.priority) - priorityIndex(b.priority) ||
      featureOrderNumber(a.slug) - featureOrderNumber(b.slug) ||
      a.slug.localeCompare(b.slug),
  );
}
