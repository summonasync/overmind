import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const widget = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-feature-widget.tsx", import.meta.url),
    "utf8",
  );
const laneCard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-card.tsx", import.meta.url), "utf8");
const laneBoard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-board.tsx", import.meta.url), "utf8");

test("lane features render as visibly bounded cards in the dashboard card language", () => {
  const source = widget();
  assert.ok(
    source.includes("rounded-lg border border-border bg-surface p-3"),
    "the same border/radius/surface family as dashboard cards, compact density",
  );
  assert.ok(source.includes("hover:border-border-strong"), "the hover accent matches the dashboard family");
});

test("the feature number renders in the app-standard key style", () => {
  assert.ok(
    widget().includes("font-mono text-sm font-semibold tracking-wide text-primary"),
    "mono violet, the way the project key heading renders in project-tabs",
  );
  assert.ok(
    !laneCard().includes("<FeatureNumberBadge") && !laneBoard().includes("<FeatureNumberBadge"),
    "the bordered badge chrome is gone from the lanes views",
  );
});

test("the widget carries number, title, progress, status hint and claim identity", () => {
  const source = widget();
  assert.ok(source.includes("{taskDone}/{taskTotal} done"), "progress reads as the dashboard counters line");
  assert.ok(source.includes('role="progressbar"'), "the bar is announced, not just drawn");
  assert.ok(source.includes("featureStatusTone(status)"), "the status hint rides the shared tone map");
  assert.ok(source.includes("{claim}"), "the holder chip is a slot the consumers fill");
});

test("lane columns and the pool both render the widget; the row markup is gone", () => {
  assert.ok(laneCard().includes("<LaneFeatureWidget"));
  assert.ok(laneBoard().includes("<LaneFeatureWidget"));
  assert.ok(!laneCard().includes("font-mono text-xs text-foreground"), "the bare slug row no longer exists");
  assert.ok(
    !laneBoard().includes("flex flex-col gap-1 rounded-md bg-surface px-3 py-2"),
    "the old flat pool row is replaced, not duplicated",
  );
});

test("the pool keeps its dashboard link; lane cards stay link-free until T1", () => {
  const board = laneBoard();
  assert.ok(
    board.includes('href={`/dashboard?project=${projectId}&feature=${item.slug}`}'),
    "the existing pool click-through survives the re-render",
  );
  assert.ok(!laneCard().includes("href="), "lane widgets expose the T1 seam, they do not pre-empt it");
});

test("the widget leaves named data seams for the T1 navigation slice", () => {
  const source = widget();
  assert.ok(source.includes("data-lane-feature-widget={slug}"), "the card root is addressable by name");
  assert.ok(source.includes("data-lane-widget-actions"), "the action strip is addressable for drag exclusions");
});
