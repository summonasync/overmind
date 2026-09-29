import { featureNumber } from "@/shared/lib/feature-number";
import type { PlanningFeatureBoardItem } from "@/features/planning/types";

export function boardItemMatchesSearch(
  item: PlanningFeatureBoardItem,
  query: string,
): boolean {
  const needle = query.trim().toLowerCase().replace(/^#/, "");
  if (needle === "") {
    return true;
  }
  const number = featureNumber(item.slug);
  return (
    (number !== null && number.includes(needle)) ||
    item.slug.toLowerCase().includes(needle) ||
    item.title.toLowerCase().includes(needle)
  );
}
