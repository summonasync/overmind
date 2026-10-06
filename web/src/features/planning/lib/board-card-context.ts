import type { PlanningFeatureBoardItem } from "@/features/planning/types";

export function cardBriefLine(item: PlanningFeatureBoardItem): string | null {
  const excerpt = item.briefExcerpt?.trim();
  return excerpt !== undefined && excerpt.length > 0 ? excerpt : null;
}

export function cardPhaseLabel(item: PlanningFeatureBoardItem): string | null {
  if (item.phase === null || item.phase.trim().length === 0) {
    return null;
  }
  return item.phase;
}

export function cardClaimLine(item: PlanningFeatureBoardItem): string | null {
  if (item.claimedByAlias === null) {
    return null;
  }
  return item.claimedByVendor === null
    ? `claimed by ${item.claimedByAlias}`
    : `claimed by ${item.claimedByAlias} from ${item.claimedByVendor}`;
}

export function cardClaimActive(item: PlanningFeatureBoardItem): boolean {
  return item.claimedByHuman || item.claimState === "alive";
}
