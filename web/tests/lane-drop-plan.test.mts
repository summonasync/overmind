import { strict as assert } from "node:assert";
import { test } from "node:test";
import { planLaneDrop, laneBandAnchor, laneBlockMessage } from "../src/features/lanes/lib/lane-drop-plan.ts";
import { resolveLaneDrop } from "../src/features/lanes/lib/lane-drop-resolution.ts";
import {
  laneDroppableData,
  laneDragTransform,
  laneFeatureDragData,
  parseLaneFeatureDragId,
  poolDroppableData,
  laneDropTargetFromOver,
} from "../src/features/lanes/lib/lane-dnd-data.ts";
import type { LaneDropContext } from "../src/features/lanes/lib/lane-drop-plan.ts";

const SLICE = ["156-dnd", "143-lanes", "148-board", "151-replies"];

function context(overrides: Partial<LaneDropContext> = {}): LaneDropContext {
  return {
    slice: SLICE,
    positionBySlug: new Map([
      ["156-dnd", 4],
      ["143-lanes", 1],
      ["148-board", 2],
      ["151-replies", 3],
    ]),
    priorityBySlug: new Map([
      ["156-dnd", "p2"],
      ["143-lanes", "p2"],
      ["148-board", "p2"],
      ["151-replies", "p2"],
    ]),
    ...overrides,
  };
}

const noBlock = null;
const noDragInFlight = false;

test("a drop onto a same-band lane-mate plans a queue-move at the mate's band slot", () => {
  const drag = laneFeatureDragData("156-dnd", "web");
  const action = resolveLaneDrop({ drag, target: laneDroppableData("web"), toIndex: 3 });
  const plan = planLaneDrop({
    action,
    overSlug: "151-replies",
    context: context(),
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(plan, {
    kind: "queue-move",
    slug: "156-dnd",
    anchorSlug: "151-replies",
    position: 3,
  });
});

test("a drop onto a cross-priority mate clamps to the nearest same-band card", () => {
  const mixed = context({
    slice: ["156-dnd", "143-lanes", "148-board"],
    positionBySlug: new Map([
      ["156-dnd", 1],
      ["143-lanes", 2],
      ["148-board", 3],
    ]),
    priorityBySlug: new Map([
      ["156-dnd", "p2"],
      ["143-lanes", "p1"],
      ["148-board", "p2"],
    ]),
  });
  assert.equal(
    laneBandAnchor(mixed, "156-dnd", "143-lanes"),
    "148-board",
    "a p2 drag over the p1 card lands on the nearest p2 neighbour",
  );
  assert.equal(
    laneBandAnchor(mixed, "148-board", "143-lanes"),
    "156-dnd",
    "coming from below, the nearest same-band mate is the one above the p1 card",
  );
});

test("a drop without a same-band neighbour anywhere is a skip, never a cross-band write", () => {
  const lone = context({
    slice: ["156-dnd", "143-lanes"],
    priorityBySlug: new Map([
      ["156-dnd", "p1"],
      ["143-lanes", "p2"],
    ]),
  });
  const drag = laneFeatureDragData("156-dnd", "web");
  const plan = planLaneDrop({
    action: resolveLaneDrop({ drag, target: laneDroppableData("web"), toIndex: 1 }),
    overSlug: "143-lanes",
    context: lone,
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(plan, { kind: "skip" });
});

test("a reorder resolved onto the dragged card itself or the container is a skip", () => {
  const drag = laneFeatureDragData("156-dnd", "web");
  for (const overSlug of [null, "156-dnd"]) {
    const plan = planLaneDrop({
      action: resolveLaneDrop({ drag, target: laneDroppableData("web"), toIndex: 0 }),
      overSlug,
      context: context(),
      blocked: noBlock,
      moveInFlight: noDragInFlight,
    });
    assert.deepStrictEqual(plan, { kind: "skip" });
  }
});

test("a cross-lane drop plans a lane assign, carrying no position", () => {
  const drag = laneFeatureDragData("156-dnd", "web");
  const action = resolveLaneDrop({ drag, target: laneDroppableData("host-core"), toIndex: 1 });
  const plan = planLaneDrop({
    action,
    overSlug: "148-board",
    context: context(),
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(plan, { kind: "assign", slug: "156-dnd", lane: "host-core" });
});

test("a pool card dropped onto a lane plans the same assign", () => {
  const drag = laneFeatureDragData("160-deck", null);
  const action = resolveLaneDrop({ drag, target: laneDroppableData("host-core"), toIndex: 0 });
  const plan = planLaneDrop({
    action,
    overSlug: null,
    context: context(),
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(plan, { kind: "assign", slug: "160-deck", lane: "host-core" });
});

test("a lane card dropped onto the pool plans an unassign, pool to pool plans nothing", () => {
  const leave = planLaneDrop({
    action: resolveLaneDrop({
      drag: laneFeatureDragData("156-dnd", "web"),
      target: poolDroppableData(),
      toIndex: null,
    }),
    overSlug: null,
    context: context(),
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(leave, { kind: "unassign", slug: "156-dnd" });
  const stay = planLaneDrop({
    action: resolveLaneDrop({
      drag: laneFeatureDragData("160-deck", null),
      target: poolDroppableData(),
      toIndex: null,
    }),
    overSlug: null,
    context: context(),
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(stay, { kind: "skip" });
});

test("a busy target refuses the drop with an inline reason, for assign and reorder alike", () => {
  const drag = laneFeatureDragData("160-deck", null);
  const plan = planLaneDrop({
    action: resolveLaneDrop({ drag, target: laneDroppableData("host-core"), toIndex: 0 }),
    overSlug: null,
    context: context(),
    blocked: "claim",
    moveInFlight: noDragInFlight,
  });
  assert.equal(plan.kind, "refuse");
  assert.equal(plan.kind === "refuse" && plan.message, "Lane host-core is held by an active claim");
  const serial = planLaneDrop({
    action: resolveLaneDrop({
      drag: laneFeatureDragData("156-dnd", "web"),
      target: laneDroppableData("host-core"),
      toIndex: 1,
    }),
    overSlug: "148-board",
    context: context(),
    blocked: "serial",
    moveInFlight: noDragInFlight,
  });
  assert.equal(serial.kind, "refuse");
  assert.equal(
    serial.kind === "refuse" && serial.message,
    "Lane host-core is serial and already holds an open feature",
  );
  assert.equal(laneBlockMessage("claim", "web"), "Lane web is held by an active claim");
});

test("a drop landing while another one is still in flight is refused, not queued", () => {
  const plan = planLaneDrop({
    action: resolveLaneDrop({
      drag: laneFeatureDragData("156-dnd", "web"),
      target: laneDroppableData("web"),
      toIndex: 1,
    }),
    overSlug: "151-replies",
    context: context(),
    blocked: noBlock,
    moveInFlight: true,
  });
  assert.equal(plan.kind, "refuse");
  assert.match(plan.kind === "refuse" ? plan.message : "", /still landing/);
});

test("an anchor whose band slot was never computed is a skip", () => {
  const partial = context({
    positionBySlug: new Map([
      ["148-board", 2],
      ["151-replies", 3],
    ]),
  });
  const drag = laneFeatureDragData("156-dnd", "web");
  const plan = planLaneDrop({
    action: resolveLaneDrop({ drag, target: laneDroppableData("web"), toIndex: 0 }),
    overSlug: "143-lanes",
    context: partial,
    blocked: noBlock,
    moveInFlight: noDragInFlight,
  });
  assert.deepStrictEqual(plan, { kind: "skip" });
});

test("the over id round-trips back to its slug and strangers parse to nothing", () => {
  assert.equal(parseLaneFeatureDragId("lane-feature:156-dnd"), "156-dnd");
  assert.equal(parseLaneFeatureDragId("lane:web"), null);
  assert.equal(parseLaneFeatureDragId("lane-feature:"), null);
  assert.equal(parseLaneFeatureDragId(42), null);
  assert.equal(parseLaneFeatureDragId(null), null);
});

test("drag transforms render as pixel translations and vanish at rest", () => {
  assert.equal(laneDragTransform(null), undefined);
  assert.equal(laneDragTransform({ x: 12, y: -4 }), "translate3d(12px, -4px, 0)");
});

test("a card under the cursor resolves to its own lane as the drop target", () => {
  const overCard = laneDropTargetFromOver(laneFeatureDragData("148-board", "host-core"));
  assert.deepStrictEqual(overCard, { kind: "lane-target", laneName: "host-core" });
  assert.equal(laneDropTargetFromOver(laneFeatureDragData("160-deck", null)), null);
  assert.deepStrictEqual(laneDropTargetFromOver(poolDroppableData()), { kind: "pool-target" });
  assert.deepStrictEqual(laneDropTargetFromOver(laneDroppableData("web")), {
    kind: "lane-target",
    laneName: "web",
  });
  assert.equal(laneDropTargetFromOver(undefined), null);
});
