import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  previewBoardLane,
  previewLaneAssignment,
  reorderBandSlugs,
} from "../src/features/lanes/lib/lane-optimistic.ts";
import type { LaneFeatureSummary, LaneRow, LanesRoster } from "../src/features/lanes/types.ts";
import type { PlanningFeatureBoardItem } from "../src/features/planning/types.ts";

function feature(slug: string, status: LaneFeatureSummary["status"] = "inProgress"): LaneFeatureSummary {
  return { slug, status, claimedByAlias: null, claimedByVendor: null, claimState: null };
}

function lane(
  name: string,
  features: LaneFeatureSummary[],
  overrides: Partial<LaneRow> = {},
): LaneRow {
  return {
    name,
    contour: null,
    serialOrder: null,
    priority: null,
    openFeatureCount: features.filter((item) => item.status !== "done").length,
    holder: null,
    features,
    ...overrides,
  };
}

function roster(lanes: LaneRow[]): LanesRoster {
  return { lanes };
}

function boardItem(slug: string, overrides: Partial<PlanningFeatureBoardItem> = {}): PlanningFeatureBoardItem {
  return {
    slug,
    title: slug,
    briefExcerpt: null,
    status: "inProgress",
    phase: null,
    priority: "p2",
    queuePosition: 1,
    isBlocked: false,
    version: 1,
    specStatus: null,
    planStatus: null,
    taskTotal: 0,
    taskDone: 0,
    updatedAt: "2026-10-24T00:00:00Z",
    claimedByAlias: null,
    claimedByVendor: null,
    claimedAt: null,
    claimState: null,
    claimedByHuman: false,
    lane: null,
    ...overrides,
  };
}

test("an assign preview moves the card into the target lane and leaves the source without it", () => {
  const before = roster([
    lane("web", [feature("156-dnd")]),
    lane("host-core", [feature("148-board")]),
  ]);
  const after = previewLaneAssignment(before, "156-dnd", "host-core");
  assert.deepEqual(after.lanes[0]?.features.map((item) => item.slug), []);
  assert.deepEqual(
    after.lanes[1]?.features.map((item) => item.slug),
    ["148-board", "156-dnd"],
  );
});

test("the preview recomputes openFeatureCount on both buckets the way the server does", () => {
  const before = roster([
    lane("web", [feature("156-dnd"), feature("143-lanes", "done")]),
    lane("host-core", []),
  ]);
  const after = previewLaneAssignment(before, "156-dnd", "host-core");
  assert.equal(after.lanes[0]?.openFeatureCount, 0, "a done feature does not count as open");
  assert.equal(after.lanes[1]?.openFeatureCount, 1);
});

test("an unassign preview empties the lane without inventing a pool bucket", () => {
  const before = roster([lane("web", [feature("156-dnd")])]);
  const after = previewLaneAssignment(before, "156-dnd", null);
  assert.equal(after.lanes.length, 1);
  assert.equal(after.lanes[0]?.features.length, 0);
});

test("the preview is a no-op when the slug is unknown or the target lane does not exist", () => {
  const before = roster([lane("web", [feature("156-dnd")])]);
  assert.deepEqual(previewLaneAssignment(before, "missing", "host-core"), before);
  assert.deepEqual(previewLaneAssignment(before, "156-dnd", "ghost-lane"), before);
});

test("the board preview flips only the dropped feature's lane, on a copy", () => {
  const board = [
    boardItem("156-dnd", { lane: null }),
    boardItem("148-board", { lane: "host-core" }),
  ];
  const after = previewBoardLane(board, "156-dnd", "host-core");
  assert.equal(after[0]?.lane, "host-core");
  assert.equal(after[1]?.lane, "host-core");
  assert.notEqual(after[0], board[0], "the preview copies the item instead of mutating the cache");
  assert.equal(board[0]?.lane, null, "the input array the cache holds stays untouched");
  assert.deepEqual(previewBoardLane(board, "156-dnd", null)[0]?.lane, null);
});

test("reordering band slugs splices the drag out and in at the anchor's slot", () => {
  const band = ["143-lanes", "148-board", "151-replies", "156-dnd"];
  assert.deepEqual(reorderBandSlugs(band, "156-dnd", "143-lanes"), [
    "156-dnd",
    "143-lanes",
    "148-board",
    "151-replies",
  ]);
  assert.deepEqual(reorderBandSlugs(band, "143-lanes", "151-replies"), [
    "148-board",
    "151-replies",
    "143-lanes",
    "156-dnd",
  ]);
});

test("a band reorder onto itself, an unknown slug, or across bands is refused", () => {
  const band = ["143-lanes", "148-board"];
  assert.equal(reorderBandSlugs(band, "143-lanes", "143-lanes"), null);
  assert.equal(reorderBandSlugs(band, "ghost", "148-board"), null);
  assert.equal(reorderBandSlugs(band, "143-lanes", "ghost"), null);
  assert.deepEqual(reorderBandSlugs(band, "143-lanes", "148-board"), ["148-board", "143-lanes"]);
});
