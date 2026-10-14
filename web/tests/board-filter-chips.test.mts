import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  EMPTY_BOARD_FILTERS,
  filterBoardItems,
  parseBoardFilters,
  toggleBoardFastChip,
  toggleBoardPriorityFilter,
  toggleBoardStatusFilter,
  writeBoardFilterParams,
} from "../src/features/planning/lib/board-filters.ts";
import type { PlanningBoardFilters } from "../src/features/planning/lib/board-filters.ts";
import type { PlanningFeatureBoardItem } from "../src/features/planning/types.ts";

const item = (overrides: Partial<PlanningFeatureBoardItem>): PlanningFeatureBoardItem => ({
  slug: "042-demo",
  title: "Demo feature",
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
  updatedAt: "2026-10-13T12:00:00Z",
  claimedByAlias: null,
  claimedByVendor: null,
  claimedAt: null,
  claimState: null,
  claimedByHuman: false,
  ...overrides,
});

test("current chip flips pinnedOnly and back", () => {
  const pinned = toggleBoardFastChip(EMPTY_BOARD_FILTERS, "current");
  assert.equal(pinned.pinnedOnly, true);
  assert.equal(toggleBoardFastChip(pinned, "current").pinnedOnly, false);
});

test("hide done chip starts on and flips off", () => {
  assert.equal(EMPTY_BOARD_FILTERS.hideDone, true);
  const shown = toggleBoardFastChip(EMPTY_BOARD_FILTERS, "hideDone");
  assert.equal(shown.hideDone, false);
  assert.equal(toggleBoardFastChip(shown, "hideDone").hideDone, true);
});

test("blocked chip flips blockedOnly and back", () => {
  const blocked = toggleBoardFastChip(EMPTY_BOARD_FILTERS, "blocked");
  assert.equal(blocked.blockedOnly, true);
  assert.equal(toggleBoardFastChip(blocked, "blocked").blockedOnly, false);
});

test("a chip toggle leaves the other chips untouched", () => {
  const next = toggleBoardFastChip({ ...EMPTY_BOARD_FILTERS, blockedOnly: true }, "current");
  assert.deepEqual(
    [next.pinnedOnly, next.hideDone, next.blockedOnly],
    [true, true, true],
  );
});

test("status toggle adds then removes without touching other filters", () => {
  const added = toggleBoardStatusFilter(EMPTY_BOARD_FILTERS, "draft");
  assert.deepEqual(added.statuses, ["draft"]);
  assert.equal(added.pinnedOnly, false);
  const removed = toggleBoardStatusFilter(added, "draft");
  assert.deepEqual(removed.statuses, []);
  assert.deepEqual(toggleBoardStatusFilter(added, "ready").statuses, ["draft", "ready"]);
});

test("priority toggle adds then removes without touching other filters", () => {
  const added = toggleBoardPriorityFilter(EMPTY_BOARD_FILTERS, "p0");
  assert.deepEqual(added.priorities, ["p0"]);
  assert.equal(added.hideDone, true);
  assert.deepEqual(toggleBoardPriorityFilter(added, "p0").priorities, []);
});

test("the current chip composes AND with a status filter", () => {
  const board = [
    item({ slug: "001-pinned-draft", claimedByAlias: "Dan", status: "draft" }),
    item({ slug: "002-anon-draft", status: "draft" }),
    item({ slug: "003-pinned-done", claimedByAlias: "Dan", status: "done" }),
  ];
  const filters: PlanningBoardFilters = {
    ...toggleBoardFastChip(EMPTY_BOARD_FILTERS, "current"),
    statuses: ["draft"],
  };
  assert.deepEqual(
    filterBoardItems(board, filters).map((entry) => entry.slug),
    ["001-pinned-draft"],
  );
});

test("hide done and blocked chips compose with the priority filter", () => {
  const board = [
    item({ slug: "010-blocked-p0", isBlocked: true, priority: "p0" }),
    item({ slug: "011-open-p0", priority: "p0" }),
    item({ slug: "012-blocked-p1", isBlocked: true, priority: "p1" }),
  ];
  const filters: PlanningBoardFilters = {
    ...toggleBoardFastChip(toggleBoardFastChip(EMPTY_BOARD_FILTERS, "blocked"), "hideDone"),
    priorities: ["p0"],
  };
  assert.deepEqual(
    filterBoardItems(board, filters).map((entry) => entry.slug),
    ["010-blocked-p0"],
  );
});

test("chip state survives a url round-trip", () => {
  const shapes: PlanningBoardFilters[] = [
    EMPTY_BOARD_FILTERS,
    toggleBoardFastChip(EMPTY_BOARD_FILTERS, "current"),
    toggleBoardFastChip(EMPTY_BOARD_FILTERS, "hideDone"),
    toggleBoardFastChip(EMPTY_BOARD_FILTERS, "blocked"),
    {
      statuses: ["draft", "ready"],
      priorities: ["p0", "p3"],
      spec: "validated",
      plan: "none",
      pinnedOnly: true,
      hideDone: false,
      blockedOnly: true,
      attentionOnly: true,
    },
  ];
  for (const shape of shapes) {
    const params = new URLSearchParams("project=abc");
    writeBoardFilterParams(params, shape);
    assert.deepEqual(parseBoardFilters(params), shape);
  }
});

test("foreign params survive a filter write", () => {
  const params = new URLSearchParams("project=abc&feature=042-demo");
  writeBoardFilterParams(params, toggleBoardFastChip(EMPTY_BOARD_FILTERS, "current"));
  assert.equal(params.get("project"), "abc");
  assert.equal(params.get("feature"), "042-demo");
  assert.equal(params.get("pinned"), "1");
});

test("default hide done writes no hidedone param, opt-out writes 0", () => {
  const on = new URLSearchParams();
  writeBoardFilterParams(on, EMPTY_BOARD_FILTERS);
  assert.equal(on.get("hidedone"), null);

  const off = new URLSearchParams();
  writeBoardFilterParams(off, toggleBoardFastChip(EMPTY_BOARD_FILTERS, "hideDone"));
  assert.equal(off.get("hidedone"), "0");
});
