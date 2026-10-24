export const LANE_DND_FEATURE = "lane-feature";
export const LANE_DND_LANE = "lane-target";
export const LANE_DND_POOL = "pool-target";

export const POOL_DROPPABLE_ID = "lanes:pool";

export interface LaneFeatureDragData {
  kind: typeof LANE_DND_FEATURE;
  slug: string;
  fromLane: string | null;
}

export interface LaneDroppableData {
  kind: typeof LANE_DND_LANE;
  laneName: string;
}

export interface PoolDroppableData {
  kind: typeof LANE_DND_POOL;
}

export type LaneDropTargetData = LaneDroppableData | PoolDroppableData;

export function laneFeatureDragId(slug: string): string {
  return `${LANE_DND_FEATURE}:${slug}`;
}

export function laneDroppableId(laneName: string): string {
  return `lane:${laneName}`;
}

export function laneFeatureDragData(slug: string, fromLane: string | null): LaneFeatureDragData {
  return { kind: LANE_DND_FEATURE, slug, fromLane };
}

export function laneDroppableData(laneName: string): LaneDroppableData {
  return { kind: LANE_DND_LANE, laneName };
}

export function poolDroppableData(): PoolDroppableData {
  return { kind: LANE_DND_POOL };
}

function asRecord(data: unknown): Record<string, unknown> | null {
  return typeof data === "object" && data !== null ? (data as Record<string, unknown>) : null;
}

export function parseLaneFeatureDrag(data: unknown): LaneFeatureDragData | null {
  const source = asRecord(data);
  if (source === null || source.kind !== LANE_DND_FEATURE) {
    return null;
  }
  if (typeof source.slug !== "string" || source.slug.length === 0) {
    return null;
  }
  if (source.fromLane !== null && typeof source.fromLane !== "string") {
    return null;
  }
  return { kind: LANE_DND_FEATURE, slug: source.slug, fromLane: source.fromLane as string | null };
}

export function parseLaneDropTarget(data: unknown): LaneDropTargetData | null {
  const target = asRecord(data);
  if (target === null) {
    return null;
  }
  if (target.kind === LANE_DND_LANE) {
    return typeof target.laneName === "string" && target.laneName.length > 0
      ? { kind: LANE_DND_LANE, laneName: target.laneName }
      : null;
  }
  return target.kind === LANE_DND_POOL ? { kind: LANE_DND_POOL } : null;
}

export function describeLaneDropTarget(target: LaneDropTargetData): string {
  return target.kind === LANE_DND_LANE ? `lane ${target.laneName}` : "the unassigned pool";
}
