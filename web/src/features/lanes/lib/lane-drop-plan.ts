import type { PlanningFeaturePriority } from "@/features/planning";
import { laneDropPermission } from "./lane-drop-resolution.ts";
import type { LaneDropAction, LaneDropBlockReason } from "./lane-drop-resolution.ts";
import type { LaneRow } from "@/features/lanes/types";

export type LaneDropPlan =
  | { kind: "queue-move"; slug: string; anchorSlug: string; position: number }
  | { kind: "assign"; slug: string; lane: string }
  | { kind: "unassign"; slug: string }
  | { kind: "refuse"; slug: string; message: string }
  | { kind: "skip" };

export const LANE_DROP_BUSY_MESSAGE = "Another drop is still landing — wait for the board to settle";

const BLOCK_MESSAGES: Record<LaneDropBlockReason, (laneName: string) => string> = {
  claim: (laneName) => `Lane ${laneName} is held by an active claim`,
  serial: (laneName) => `Lane ${laneName} is serial and already holds an open feature`,
};

export function laneBlockMessage(reason: LaneDropBlockReason, laneName: string): string {
  return BLOCK_MESSAGES[reason](laneName);
}

export interface LaneDropContext {
  slice: readonly string[];
  positionBySlug: ReadonlyMap<string, number>;
  priorityBySlug: ReadonlyMap<string, PlanningFeaturePriority>;
}

interface LaneDropPlanInput {
  action: LaneDropAction;
  overSlug: string | null;
  context: LaneDropContext;
  blocked: LaneDropBlockReason | null;
  moveInFlight: boolean;
}

export const EMPTY_LANE_DROP_CONTEXT: LaneDropContext = {
  slice: [],
  positionBySlug: new Map(),
  priorityBySlug: new Map(),
};

export function laneDropBlockReason(lane: LaneRow, draggedSlug: string): LaneDropBlockReason | null {
  const permission = laneDropPermission(lane, draggedSlug);
  return permission.allowed ? null : permission.reason;
}

// The anchor is the same-band card closest to where the drop landed: a p2 card released over a p0
// mate reorders inside its own band, because a queue-move addresses one band's slots and dragging
// cannot change priority. Distance ties resolve upward, matching where the slot visually sits.
export function laneBandAnchor(
  context: LaneDropContext,
  dragSlug: string,
  overSlug: string,
): string | null {
  const dragPriority = context.priorityBySlug.get(dragSlug);
  if (dragPriority === undefined) {
    return null;
  }
  const overIndex = context.slice.indexOf(overSlug);
  if (overIndex === -1) {
    return null;
  }
  const matchesPriority = (slug: string | undefined): slug is string =>
    slug !== undefined && slug !== dragSlug && context.priorityBySlug.get(slug) === dragPriority;
  if (matchesPriority(context.slice[overIndex])) {
    return context.slice[overIndex];
  }
  for (let radius = 1; radius < context.slice.length; radius++) {
    const above = context.slice[overIndex - radius];
    if (matchesPriority(above)) {
      return above;
    }
    const below = context.slice[overIndex + radius];
    if (matchesPriority(below)) {
      return below;
    }
  }
  return null;
}

export function planLaneDrop({ action, overSlug, context, blocked, moveInFlight }: LaneDropPlanInput): LaneDropPlan {
  if (action.type === "none") {
    return { kind: "skip" };
  }
  if (moveInFlight) {
    return { kind: "refuse", slug: action.slug, message: LANE_DROP_BUSY_MESSAGE };
  }
  if (action.type === "unassign") {
    return { kind: "unassign", slug: action.slug };
  }
  if (action.type === "assign" || action.type === "transfer") {
    const lane = action.type === "assign" ? action.laneName : action.toLane;
    if (blocked !== null) {
      return { kind: "refuse", slug: action.slug, message: laneBlockMessage(blocked, lane) };
    }
    return { kind: "assign", slug: action.slug, lane };
  }
  if (blocked !== null) {
    return {
      kind: "refuse",
      slug: action.slug,
      message: laneBlockMessage(blocked, action.laneName),
    };
  }
  if (overSlug === null || overSlug === action.slug) {
    return { kind: "skip" };
  }
  const anchor = laneBandAnchor(context, action.slug, overSlug);
  const position = anchor === null ? undefined : context.positionBySlug.get(anchor);
  if (anchor === null || position === undefined) {
    return { kind: "skip" };
  }
  return { kind: "queue-move", slug: action.slug, anchorSlug: anchor, position };
}
