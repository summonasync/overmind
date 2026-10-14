import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  EMPTY_BOARD_FILTERS,
  filterBoardItems,
  isAttentionItem,
  isBoardFilterActive,
  coerceBoardFilters,
  parseBoardFilters,
  writeBoardFilterParams,
} from "../src/features/planning/lib/board-filters.ts";
import type { BoardAttentionContext, PlanningBoardFilters } from "../src/features/planning/lib/board-filters.ts";
import type { PlanningFeatureBoardItem } from "../src/features/planning/types.ts";

const item = (overrides: Partial<PlanningFeatureBoardItem>): PlanningFeatureBoardItem => ({
  slug: "085-demo",
  title: "Demo feature",
  briefExcerpt: null,
  status: "inProgress",
  phase: null,
  priority: "p2",
  queuePosition: 1,
  isBlocked: false,
  version: 1,
  specStatus: "validated",
  planStatus: "validated",
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

const quiet = (overrides: Partial<BoardAttentionContext> = {}): BoardAttentionContext => ({
  awaitingFeatureSlugs: new Set(),
  humanValidationOn: false,
  ...overrides,
});

test("review status alone puts a feature in require attention", () => {
  assert.equal(isAttentionItem(item({ status: "review" }), quiet()), true);
});

test("an awaiting approval or question notification alone puts a feature in require attention", () => {
  const notified = quiet({ awaitingFeatureSlugs: new Set(["085-demo"]) });
  assert.equal(isAttentionItem(item({ status: "inProgress" }), notified), true);
});

test("a draft spec counts only while the human-validation gate is on", () => {
  const gated = quiet({ humanValidationOn: true });
  assert.equal(isAttentionItem(item({ specStatus: "draft" }), gated), true);
  assert.equal(isAttentionItem(item({ planStatus: "draft" }), gated), true);
  assert.equal(isAttentionItem(item({ specStatus: "draft" }), quiet()), false);
  assert.equal(isAttentionItem(item({ planStatus: "draft" }), quiet()), false);
  assert.equal(isAttentionItem(item(), gated), false);
});

test("signals union: all three fire on one row", () => {
  const everything = quiet({ awaitingFeatureSlugs: new Set(["085-demo"]), humanValidationOn: true });
  assert.equal(
    isAttentionItem(item({ status: "review", specStatus: "draft", planStatus: "draft" }), everything),
    true,
  );
});

test("empty signals keep a row out of require attention", () => {
  assert.equal(isAttentionItem(item({ status: "ready" }), quiet({ humanValidationOn: true })), false);
});

test("the attention chip keeps only attention rows", () => {
  const board = [
    item({ slug: "085-review", status: "review" }),
    item({ slug: "084-notified", status: "ready" }),
    item({ slug: "083-gated-draft", status: "draft", specStatus: "draft" }),
    item({ slug: "082-quiet", status: "inProgress" }),
  ];
  const context = quiet({
    awaitingFeatureSlugs: new Set(["084-notified"]),
    humanValidationOn: true,
  });
  assert.deepEqual(
    filterBoardItems(board, { ...EMPTY_BOARD_FILTERS, attentionOnly: true }, context).map(
      (entry) => entry.slug,
    ),
    ["085-review", "084-notified", "083-gated-draft"],
  );
});

test("the attention chip composes AND with row-1 filters", () => {
  const board = [
    item({ slug: "085-review-p1", status: "review", priority: "p1" }),
    item({ slug: "082-quiet-p0", status: "inProgress", priority: "p0" }),
    item({ slug: "081-notified-p2", status: "ready", priority: "p2" }),
  ];
  const context = quiet({ awaitingFeatureSlugs: new Set(["081-notified-p2"]) });
  const filters: PlanningBoardFilters = {
    ...EMPTY_BOARD_FILTERS,
    attentionOnly: true,
    priorities: ["p1"],
  };
  assert.deepEqual(
    filterBoardItems(board, filters, context).map((entry) => entry.slug),
    ["085-review-p1"],
  );
});

test("e2e pin: attention=1 in the url surfaces a review-status row", () => {
  const params = new URLSearchParams("project=abc&attention=1");
  const filters = parseBoardFilters(params);
  assert.equal(filters.attentionOnly, true);
  const board = [
    item({ slug: "085-review-row", status: "review" }),
    item({ slug: "085-quiet-row", status: "inProgress" }),
  ];
  assert.deepEqual(
    filterBoardItems(board, filters, quiet({ humanValidationOn: true })).map(
      (entry) => entry.slug,
    ),
    ["085-review-row"],
  );
});

test("attention chip state survives a url round-trip", () => {
  const on = new URLSearchParams();
  writeBoardFilterParams(on, { ...EMPTY_BOARD_FILTERS, attentionOnly: true });
  assert.equal(on.get("attention"), "1");
  assert.equal(parseBoardFilters(on).attentionOnly, true);

  const off = new URLSearchParams();
  writeBoardFilterParams(off, EMPTY_BOARD_FILTERS);
  assert.equal(off.get("attention"), null);
  assert.equal(parseBoardFilters(off).attentionOnly, false);
});

test("stored board filters keep the attention flag, legacy shapes default it off", () => {
  assert.equal(coerceBoardFilters({ attentionOnly: true })?.attentionOnly, true);
  assert.equal(coerceBoardFilters({})?.attentionOnly, false);
  assert.equal(EMPTY_BOARD_FILTERS.attentionOnly, false);
});

test("an active attention chip marks the filter bar active", () => {
  assert.equal(isBoardFilterActive(EMPTY_BOARD_FILTERS), false);
  assert.equal(isBoardFilterActive({ ...EMPTY_BOARD_FILTERS, attentionOnly: true }), true);
});

test("the planning page wires the chip-gated awaiting window and the workflow gate into the board filter", () => {
  const source = readFileSync(
    new URL("../src/pages/planning-page/ui/planning-page.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(source.includes("useAwaitingNotifications(boardFilters.filters.attentionOnly)"));
  assert.ok(!source.includes("useNotifications("));
  assert.ok(source.includes("usePlanningWorkflow(projectId)"));
  assert.ok(source.includes("effectiveRequireHumanValidation"));
  assert.match(
    source,
    /filterBoardItems\(searchedFeatures, boardFilters\.filters, attention\)/,
  );
});

test("the awaiting query skips fetching while the attention chip is off and filters to awaiting items", () => {
  const source = readFileSync(
    new URL("../src/features/notifications/lib/use-awaiting-notifications.ts", import.meta.url),
    "utf8",
  );
  assert.ok(source.includes("export function useAwaitingNotifications(enabled = true)"));
  assert.ok(source.includes("{ ...awaitingNotificationsQueryOptions(), enabled }"));
  assert.ok(source.includes(".filter(isAwaitingNotification)"));
});

test("the awaiting query reads the wide take window, not the capped bell list", () => {
  const source = readFileSync(
    new URL("../src/features/notifications/queries.ts", import.meta.url),
    "utf8",
  );
  assert.ok(source.includes("AWAITING_TAKE = 200"));
  assert.ok(source.includes("listNotifications({ awaiting: true, take: AWAITING_TAKE })"));
});

test("the toolbar exposes the attention chip next to the fast chips", () => {
  const source = readFileSync(
    new URL("../src/features/planning/components/board-filter-bar.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(source.includes('aria-label="Require attention filter"'));
  assert.ok(source.includes("attentionOnly: !filters.attentionOnly"));
});
