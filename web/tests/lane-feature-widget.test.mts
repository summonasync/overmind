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

test("the pool keeps its dashboard link; lane cards join it through the same helper", () => {
  const board = laneBoard();
  const card = laneCard();
  assert.ok(
    board.includes("href={featureHref(projectId, item.slug)}"),
    "the pool click-through survives the re-render, now via the shared route builder",
  );
  assert.ok(
    card.includes("href={featureHref(projectId, feature.slug)}"),
    "lane cards pick up the T1 deep link",
  );
});

test("the widget leaves named data seams for the T1 navigation slice", () => {
  const source = widget();
  assert.ok(source.includes("data-lane-feature-widget={slug}"), "the card root is addressable by name");
  assert.ok(source.includes("data-lane-widget-actions"), "the action strip is addressable for drag exclusions");
});

test("the number is a deep link: click navigates, the press never reaches the drag sensors", () => {
  const source = widget();
  assert.ok(source.includes("href={href}"), "the key navigates to the route the consumer hands in");
  assert.ok(
    source.includes("onPointerDown={keepPressLocal}"),
    "the anchor press is excluded from the drag sensors per element",
  );
  assert.ok(
    source.includes("onClick={keepPressLocal}"),
    "the click stops at the anchor, no upstream listener reacts to it",
  );
});

test("a press on the card body stays drag territory: the title is plain text, not a link", () => {
  const source = widget();
  assert.ok(
    source.includes(
      '<span className="min-w-0 truncate text-sm font-semibold text-foreground">{label}</span>',
    ),
    "no navigation affordance on the body, a threshold press there drags instead",
  );
  const rootLine = source.split("\n").find((line) => line.includes("data-lane-feature-widget"));
  assert.ok(
    rootLine !== undefined && !rootLine.includes("onPointerDown"),
    "the drag exclusion is per affordance element, the card root still feeds the sensors",
  );
});

test("the chevron menu rides the widget and carries the claim identity for its readout", () => {
  const source = widget();
  assert.ok(source.includes("<LaneWidgetMenu"), "the affordance zone lives with the card");
  assert.ok(
    source.includes("href !== null ? ("),
    "a widget without a route keeps neither link nor menu",
  );
  assert.ok(
    source.includes("claimAlias={claimAlias}") && source.includes("claimState={claimState}"),
    "the menu readout gets the holder alias and claim state from the consumers",
  );
});
