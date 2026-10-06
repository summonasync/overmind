import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  cardBriefLine,
  cardClaimActive,
  cardClaimLine,
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

test("every server phase label passes through", () => {
  for (const label of [
    "drafting",
    "writing spec",
    "planning",
    "plan validated",
    "in progress",
    "review",
    "done",
  ]) {
    assert.equal(cardPhaseLabel(item({ phase: label })), label);
  }
});

test("null phase renders no phase label", () => {
  assert.equal(cardPhaseLabel(item({ phase: null })), null);
});

test("blank phase (empty or whitespace) renders no phase label", () => {
  assert.equal(cardPhaseLabel(item({ phase: "" })), null);
  assert.equal(cardPhaseLabel(item({ phase: "   " })), null);
});

test("done cards keep the single done representation via the phase label", () => {
  const done = item({ status: "done", phase: "done", taskTotal: 3, taskDone: 3 });
  assert.equal(cardPhaseLabel(done), "done");
  assert.notEqual(cardPhaseLabel(done), null);
});

test("claim line uses the exact alias-from-vendor format", () => {
  const claimed = item({ claimedByAlias: "Van", claimedByVendor: "claude", claimState: "alive" });
  assert.equal(cardClaimLine(claimed), "claimed by Van from claude");
});

test("unclaimed card renders no claim line", () => {
  assert.equal(cardClaimLine(item({})), null);
});

test("null vendor renders the alias without a dangling from tail", () => {
  const claimed = item({ claimedByAlias: "Van", claimedByVendor: null, claimState: "alive" });
  assert.equal(cardClaimLine(claimed), "claimed by Van");
  assert.ok(!cardClaimLine(claimed)!.includes("null"));
});

test("claim is active when the claim state is alive", () => {
  assert.equal(cardClaimActive(item({ claimedByAlias: "Van", claimedByVendor: "claude", claimState: "alive" })), true);
});

test("stale and ended claims are not active", () => {
  assert.equal(cardClaimActive(item({ claimedByAlias: "Van", claimedByVendor: "claude", claimState: "stale" })), false);
  assert.equal(cardClaimActive(item({ claimedByAlias: "Van", claimedByVendor: "claude", claimState: "ended" })), false);
});

test("null claim state is not active", () => {
  assert.equal(cardClaimActive(item({ claimedByAlias: "Van", claimedByVendor: "claude", claimState: null })), false);
});

test("human-held claim is active regardless of the claim state", () => {
  assert.equal(cardClaimActive(item({ claimedByAlias: "Johan", claimedByVendor: null, claimState: null, claimedByHuman: true })), true);
  assert.equal(cardClaimActive(item({ claimedByAlias: "Johan", claimedByVendor: "claude", claimState: "ended", claimedByHuman: true })), true);
});

test("unclaimed card is never active", () => {
  assert.equal(cardClaimActive(item({})), false);
});
