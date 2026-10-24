import type { LaneRow } from "@/features/lanes/types";
import type { LaneFeatureDragData, LaneDropTargetData } from "./lane-dnd-data.ts";
import { LANE_DND_POOL } from "./lane-dnd-data.ts";

export type LaneDropAction =
  | { type: "reorder"; slug: string; laneName: string; toIndex: number }
  | { type: "transfer"; slug: string; fromLane: string; toLane: string; toIndex: number }
  | { type: "assign"; slug: string; laneName: string; toIndex: number }
  | { type: "unassign"; slug: string }
  | { type: "none" };

export type LaneDropBlockReason = "claim" | "serial";

export type LaneDropPermission = { allowed: true } | { allowed: false; reason: LaneDropBlockReason };

interface LaneDropInput {
  drag: LaneFeatureDragData | null;
  target: LaneDropTargetData | null;
  toIndex?: number | null;
}

export function resolveLaneDrop({ drag, target, toIndex }: LaneDropInput): LaneDropAction {
  if (drag === null || target === null) {
    return { type: "none" };
  }
  if (target.kind === LANE_DND_POOL) {
    return drag.fromLane === null ? { type: "none" } : { type: "unassign", slug: drag.slug };
  }
  if (typeof toIndex !== "number" || !Number.isInteger(toIndex) || toIndex < 0) {
    return { type: "none" };
  }
  if (drag.fromLane === null) {
    return { type: "assign", slug: drag.slug, laneName: target.laneName, toIndex };
  }
  if (drag.fromLane === target.laneName) {
    return { type: "reorder", slug: drag.slug, laneName: target.laneName, toIndex };
  }
  return {
    type: "transfer",
    slug: drag.slug,
    fromLane: drag.fromLane,
    toLane: target.laneName,
    toIndex,
  };
}

// LaneRow.holder aggregates the one alive claim on any feature of the lane, so an alive holder
// already means a lane another agent is working in; serialOrder marks the one-open-feature-at-a-time
// contour, where a second open feature (the dragged one excepted) would break the lane's contract.
export function laneDropPermission(lane: LaneRow, draggedSlug: string): LaneDropPermission {
  if (lane.holder !== null && lane.holder.claimState === "alive") {
    return { allowed: false, reason: "claim" };
  }
  const holdsOtherOpenFeature = lane.features.some(
    (feature) => feature.slug !== draggedSlug && feature.status !== "done",
  );
  if (lane.serialOrder !== null && holdsOtherOpenFeature) {
    return { allowed: false, reason: "serial" };
  }
  return { allowed: true };
}
