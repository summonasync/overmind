import { featureResultTabHref } from "./done-gate.ts";
import type { PlanningInboxItem } from "../types";

export function inboxRowHref(projectId: string, item: PlanningInboxItem): string {
  if (item.link !== null && item.link.length > 0) {
    return item.link;
  }
  return featureResultTabHref(projectId, item.slug);
}

export function inboxHolderLabel(item: PlanningInboxItem): string | null {
  if (item.claimState === "released") {
    return "released";
  }
  return item.claimedByAlias;
}
