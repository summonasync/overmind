import type { PlanningFeatureStatus } from "@/features/planning";

export type LaneClaimState = "alive" | "stale" | "ended";

export interface LaneHolder {
  alias: string;
  vendor: string;
  claimState: LaneClaimState;
}

export interface LaneFeatureSummary {
  slug: string;
  status: PlanningFeatureStatus;
  claimedByAlias: string | null;
  claimedByVendor: string | null;
  claimState: LaneClaimState | null;
}

export interface LaneRow {
  name: string;
  contour: string | null;
  serialOrder: number | null;
  priority: number | null;
  openFeatureCount: number;
  holder: LaneHolder | null;
  features: LaneFeatureSummary[];
}

export interface LanesRoster {
  lanes: LaneRow[];
}

export interface LaneMutableFields {
  contour: string | null;
  serialOrder: number | null;
  priority: number | null;
}

export interface LaneUpsertInput extends LaneMutableFields {
  name: string;
}
