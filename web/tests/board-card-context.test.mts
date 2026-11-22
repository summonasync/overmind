import { strict as assert } from "node:assert";
import { test } from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { createJiti } from "jiti";
import {
  cardBriefLine,
  cardClaimActive,
  cardClaimLine,
  cardPhaseLabel,
} from "../src/features/planning/lib/board-card-context.ts";
import type { PlanningClaimState, PlanningFeatureBoardItem } from "../src/features/planning/types.ts";
import type { LaneClaimState } from "../src/features/lanes/types.ts";

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

test("released orphans are not active", () => {
  assert.equal(cardClaimActive(item({ claimedByAlias: "Van", claimedByVendor: "claude", claimState: "released" })), false);
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

const jiti = createJiti(import.meta.url, {
  jsx: { runtime: "automatic", importSource: "react" },
  alias: { "@": new URL("../src/", import.meta.url).href },
});
const { FeatureClaimChip } = await jiti.import(
  "../src/features/planning/components/feature-claim-chip.tsx",
);
const { LaneClaimChip } = await jiti.import("../src/features/lanes/components/lane-claim-chip.tsx");

const claimed = {
  slug: "042-demo",
  claimedByAlias: "Van",
  claimedByVendor: "claude",
  claimedAt: "2026-11-21T10:00:00Z",
  claimedByHuman: false,
};

const renderFeatureChip = (claimState: PlanningClaimState | null) =>
  renderToStaticMarkup(React.createElement(FeatureClaimChip, { item: { ...claimed, claimState } }));
const renderLaneChip = (claimState: LaneClaimState | null) =>
  renderToStaticMarkup(React.createElement(LaneClaimChip, { alias: "Van", vendor: "claude", claimState }));

test("the feature chip renders released orphans on the dashed released arm, never the alive arm", () => {
  const html = renderFeatureChip("released");
  assert.ok(html.includes('data-claim-state="released"'), "the orphan gets its own arm");
  assert.ok(html.includes("border-dashed"), "released borrows the app's dashed orphan language");
  assert.ok(html.includes("released"), "the label names the wire vocabulary verbatim");
  assert.ok(html.includes("can be claimed again"), "the sr-only note explains the orphan semantics");
  assert.ok(!html.includes("bg-success"), "the alive styling must never dress a released claim");
  assert.ok(!html.includes('data-claim-state="alive"'), "no live dot for an orphan");
});

test("the lane chip renders released orphans on the dashed released arm, never the alive arm", () => {
  const html = renderLaneChip("released");
  assert.ok(html.includes('data-claim-state="released"'), "the orphan gets its own arm");
  assert.ok(html.includes("border-dashed"), "released borrows the app's dashed orphan language");
  assert.ok(html.includes("released"), "the label names the wire vocabulary verbatim");
  assert.ok(!html.includes("bg-success"), "the alive styling must never dress a released claim");
  assert.ok(!html.includes('data-claim-state="alive"'), "no live dot for an orphan");
});

test("dead holders arrive as ended from T0's raw liveness and render the ended arm", () => {
  const featureHtml = renderFeatureChip("ended");
  assert.ok(featureHtml.includes("· ended"), "the feature chip ends the holder explicitly");
  assert.ok(!featureHtml.includes("bg-success"), "a dead holder is never styled alive");

  const laneHtml = renderLaneChip("ended");
  assert.ok(laneHtml.includes("· ended"), "the lane chip ends the holder explicitly");
  assert.ok(!laneHtml.includes("bg-success"), "a dead holder is never styled alive");
});

test("alive claims keep the live arm with its dot", () => {
  const html = renderFeatureChip("alive");
  assert.ok(html.includes('data-claim-state="alive"'));
  assert.ok(html.includes("bg-success"));
});
