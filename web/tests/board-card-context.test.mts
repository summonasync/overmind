import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  cardBriefLine,
  cardPhaseLabel,
} from "../src/features/planning/lib/board-card-context.ts";
import type { PlanningFeatureBoardItem } from "../src/features/planning/types.ts";

const item = (overrides: Partial<PlanningFeatureBoardItem>): PlanningFeatureBoardItem => ({
  slug: "042-demo",
  title: "Demo feature",
  briefExcerpt: null,
  status: "draft",
  phase: "drafting",
  priority: "p2",
  queuePosition: 1,
  isBlocked: false,
  version: 1,
  specStatus: null,
  planStatus: null,
  taskTotal: 0,
  taskDone: 0,
  updatedAt: "2026-10-04T12:00:00Z",
  claimedByAlias: null,
  claimedByVendor: null,
  claimedAt: null,
  claimState: null,
  claimedByHuman: false,
  ...overrides,
});

test("brief excerpt present yields the brief line", () => {
  assert.equal(cardBriefLine(item({ briefExcerpt: "Ship the exporter for dark mode." })), "Ship the exporter for dark mode.");
});

test("null briefExcerpt yields no brief line", () => {
  assert.equal(cardBriefLine(item({ briefExcerpt: null })), null);
});

test("bodyless feature (empty or blank excerpt) renders no brief line", () => {
  assert.equal(cardBriefLine(item({ briefExcerpt: "" })), null);
  assert.equal(cardBriefLine(item({ briefExcerpt: "   " })), null);
});

test("phase label passes the ready-made server label through", () => {
  assert.equal(cardPhaseLabel(item({ phase: "writing spec" })), "writing spec");
  assert.equal(cardPhaseLabel(item({ phase: "plan validated" })), "plan validated");
});

test("done cards keep the single done representation via the phase label", () => {
  const done = item({ status: "done", phase: "done", taskTotal: 3, taskDone: 3 });
  assert.equal(cardPhaseLabel(done), "done");
  assert.notEqual(cardPhaseLabel(done), null);
});
