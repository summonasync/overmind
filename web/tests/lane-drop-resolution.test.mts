import { strict as assert } from "node:assert";
import { test } from "node:test";
import { laneDropPermission, resolveLaneDrop } from "../src/features/lanes/lib/lane-drop-resolution.ts";
import {
  describeLaneDropTarget,
  laneDroppableData,
  laneDroppableId,
  laneFeatureDragData,
  laneFeatureDragId,
  parseLaneDropTarget,
  parseLaneFeatureDrag,
  poolDroppableData,
  POOL_DROPPABLE_ID,
} from "../src/features/lanes/lib/lane-dnd-data.ts";
import type { LaneFeatureSummary, LaneRow } from "../src/features/lanes/types.ts";

function laneFeature(
  slug: string,
  status: LaneFeatureSummary["status"] = "inProgress",
): LaneFeatureSummary {
  return { slug, status, claimedByAlias: null, claimedByVendor: null, claimState: null };
}

function laneRow(overrides: Partial<LaneRow> & Pick<LaneRow, "name">): LaneRow {
  return {
    contour: null,
    serialOrder: null,
    priority: null,
    openFeatureCount: 0,
    holder: null,
    features: [],
    ...overrides,
  };
}

test("a drop inside the origin lane resolves to a reorder at the target slot", () => {
  const action = resolveLaneDrop({
    drag: laneFeatureDragData("156-lanes-drag-and-drop", "web-boundary"),
    target: laneDroppableData("web-boundary"),
    toIndex: 2,
  });
  assert.deepStrictEqual(action, {
    type: "reorder",
    slug: "156-lanes-drag-and-drop",
    laneName: "web-boundary",
    toIndex: 2,
  });
});

test("a cross-lane drop resolves to a transfer that keeps the origin", () => {
  const action = resolveLaneDrop({
    drag: laneFeatureDragData("148-board", "web-boundary"),
    target: laneDroppableData("host-core"),
    toIndex: 0,
  });
  assert.deepStrictEqual(action, {
    type: "transfer",
    slug: "148-board",
    fromLane: "web-boundary",
    toLane: "host-core",
    toIndex: 0,
  });
});

test("a pool card dropped onto a lane resolves to an assign at the slot", () => {
  const action = resolveLaneDrop({
    drag: laneFeatureDragData("160-deck", null),
    target: laneDroppableData("web-boundary"),
    toIndex: 3,
  });
  assert.deepStrictEqual(action, {
    type: "assign",
    slug: "160-deck",
    laneName: "web-boundary",
    toIndex: 3,
  });
});

test("a lane card dropped onto the pool resolves to an unassign", () => {
  const action = resolveLaneDrop({
    drag: laneFeatureDragData("160-deck", "web-boundary"),
    target: poolDroppableData(),
    toIndex: null,
  });
  assert.deepStrictEqual(action, { type: "unassign", slug: "160-deck" });
});

test("a pool card dropped back onto the pool is a no-op", () => {
  const action = resolveLaneDrop({
    drag: laneFeatureDragData("160-deck", null),
    target: poolDroppableData(),
    toIndex: null,
  });
  assert.deepStrictEqual(action, { type: "none" });
});

test("a drop without a payload or without a target resolves to nothing", () => {
  const target = laneDroppableData("web-boundary");
  const drag = laneFeatureDragData("160-deck", null);
  assert.deepStrictEqual(resolveLaneDrop({ drag: null, target, toIndex: 0 }), { type: "none" });
  assert.deepStrictEqual(resolveLaneDrop({ drag, target: null, toIndex: 0 }), { type: "none" });
  assert.deepStrictEqual(resolveLaneDrop({ drag, target, toIndex: null }), { type: "none" });
  assert.deepStrictEqual(resolveLaneDrop({ drag, target, toIndex: undefined }), { type: "none" });
});

test("a lane slot that is not a zero-based integer is refused", () => {
  const drag = laneFeatureDragData("160-deck", null);
  const target = laneDroppableData("web-boundary");
  assert.deepStrictEqual(resolveLaneDrop({ drag, target, toIndex: -1 }), { type: "none" });
  assert.deepStrictEqual(resolveLaneDrop({ drag, target, toIndex: 1.5 }), { type: "none" });
});

test("the drag payload round-trips through its own parser", () => {
  assert.deepStrictEqual(
    parseLaneFeatureDrag(laneFeatureDragData("156-lanes-drag-and-drop", "web-boundary")),
    { kind: "lane-feature", slug: "156-lanes-drag-and-drop", fromLane: "web-boundary" },
  );
  assert.deepStrictEqual(parseLaneFeatureDrag(laneFeatureDragData("160-deck", null)), {
    kind: "lane-feature",
    slug: "160-deck",
    fromLane: null,
  });
});

test("drag parsing rejects strangers and malformed payloads", () => {
  assert.equal(parseLaneFeatureDrag(laneDroppableData("web-boundary")), null);
  assert.equal(parseLaneFeatureDrag(null), null);
  assert.equal(parseLaneFeatureDrag("lane-feature:160-deck"), null);
  assert.equal(parseLaneFeatureDrag({ slug: "160-deck", fromLane: null }), null);
  assert.equal(parseLaneFeatureDrag({ slug: "", fromLane: null }), null);
  assert.equal(parseLaneFeatureDrag({ slug: 42, fromLane: null }), null);
  assert.equal(parseLaneFeatureDrag({ slug: "160-deck", fromLane: 42 }), null);
});

test("drop-target parsing separates lanes from the pool", () => {
  assert.deepStrictEqual(parseLaneDropTarget(laneDroppableData("host-core")), {
    kind: "lane-target",
    laneName: "host-core",
  });
  assert.deepStrictEqual(parseLaneDropTarget(poolDroppableData()), { kind: "pool-target" });
  assert.equal(parseLaneDropTarget(laneFeatureDragData("160-deck", null)), null);
  assert.equal(parseLaneDropTarget(undefined), null);
  assert.equal(parseLaneDropTarget({ laneName: "" }), null);
  assert.equal(parseLaneDropTarget({ laneName: 7 }), null);
});

test("target copy names a lane by its name and the pool by its role", () => {
  assert.equal(describeLaneDropTarget(laneDroppableData("host-core")), "lane host-core");
  assert.equal(
    describeLaneDropTarget(poolDroppableData()),
    "the unassigned pool",
  );
});

test("draggable and droppable ids never collide across the two families", () => {
  const ids = [
    laneFeatureDragId("160-deck"),
    laneDroppableId("web-boundary"),
    POOL_DROPPABLE_ID,
  ];
  assert.equal(new Set(ids).size, ids.length);
  assert.ok(laneFeatureDragId("160-deck").includes("160-deck"));
  assert.ok(laneDroppableId("web-boundary").includes("web-boundary"));
});

test("a lane with an alive claim refuses the drop", () => {
  const lane = laneRow({
    name: "host-core",
    holder: { alias: "guldan", vendor: "claude", claimState: "alive" },
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), {
    allowed: false,
    reason: "claim",
  });
});

test("stale and ended claims leave the lane open", () => {
  for (const claimState of ["stale", "ended"] as const) {
    const lane = laneRow({
      name: "host-core",
      holder: { alias: "guldan", vendor: "claude", claimState },
    });
    assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), { allowed: true });
  }
});

test("an unclaimed lane is open", () => {
  const lane = laneRow({
    name: "host-core",
    features: [laneFeature("148-board")],
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), { allowed: true });
});

test("a serial lane holding another open feature refuses the drop", () => {
  const lane = laneRow({
    name: "host-core",
    serialOrder: 2,
    openFeatureCount: 1,
    features: [laneFeature("148-board")],
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), {
    allowed: false,
    reason: "serial",
  });
});

test("a serial lane whose only open feature is the dragged one accepts it back", () => {
  const lane = laneRow({
    name: "host-core",
    serialOrder: 2,
    openFeatureCount: 1,
    features: [laneFeature("160-deck")],
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), { allowed: true });
});

test("a serial lane with only closed features is open", () => {
  const lane = laneRow({
    name: "host-core",
    serialOrder: 5,
    features: [laneFeature("145-onboarding", "done"), laneFeature("143-lanes", "done")],
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), { allowed: true });
});

test("a live claim outweighs the serial rule when both bite", () => {
  const lane = laneRow({
    name: "host-core",
    serialOrder: 1,
    holder: { alias: "vega", vendor: "qwen", claimState: "alive" },
    features: [laneFeature("148-board")],
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), {
    allowed: false,
    reason: "claim",
  });
});

test("a parallel lane ignores how many open features it holds", () => {
  const lane = laneRow({
    name: "web-boundary",
    openFeatureCount: 4,
    features: [laneFeature("148-board"), laneFeature("151-replies", "review")],
  });
  assert.deepStrictEqual(laneDropPermission(lane, "160-deck"), { allowed: true });
});
