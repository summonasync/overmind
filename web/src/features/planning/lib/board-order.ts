import type {
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
} from "@/features/planning/types";
import { FEATURE_PRIORITIES } from "@/features/planning/types";

export function compareBoardItems(a: PlanningFeatureBoardItem, b: PlanningFeatureBoardItem): number {
  return (
    FEATURE_PRIORITIES.indexOf(a.priority) - FEATURE_PRIORITIES.indexOf(b.priority) ||
    a.queuePosition - b.queuePosition ||
    a.slug.localeCompare(b.slug)
  );
}

export function queueBandSlugs(
  board: PlanningFeatureBoardItem[],
  priority: PlanningFeaturePriority,
  dragSlug: string,
  targetSlug: string,
): string[] | null {
  const band = board
    .filter((item) => item.priority === priority && item.status !== "done")
    .sort(compareBoardItems);
  const from = band.findIndex((item) => item.slug === dragSlug);
  const to = band.findIndex((item) => item.slug === targetSlug);
  if (from === -1 || to === -1 || from === to) {
    return null;
  }
  const next = [...band];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next.map((item) => item.slug);
}
