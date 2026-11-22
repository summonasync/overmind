import { strict as assert } from "node:assert";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { basename, join, relative } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createJiti } from "jiti";
import {
  EMPTY_GRAPH_FILTERS,
  GRAPH_ENTRY_TYPES,
  GRAPH_QUERY_KEYS,
  isGraphFilterActive,
  parseGraphFilters,
  toggleGraphEntryType,
  withFocus,
  withOrphansOnly,
  writeGraphFilters,
} from "../src/features/journal/lib/graph-filters.ts";
import {
  GRAPH_SEARCH_HINT,
  ceilingMessage,
  clusteredNotice,
  computingAnchorsNotice,
  computingBloomNotice,
  computingLayoutNotice,
  emptyFieldMessage,
  graphAriaLabel,
  isolatedInViewLabel,
  nodeCountLabel,
  nodeKindLabel,
  noteExcerpt,
  orphanLabel,
  precomputedNotice,
  settleFailedNotice,
  stateLabel,
  tierName,
  unsupportedRowsNotice,
} from "../src/features/journal/lib/graph-copy.ts";
import {
  type LayoutNode,
  PRECOMPUTE_CEILING,
  SIM_CEILING,
  buildNeighbourhoods,
  isOverCeiling,
  layoutModeFor,
  pinNode,
  releasePinnedNodes,
  seedLayoutLinks,
  seedPositions,
  settleLayout,
  settleTicksFor,
  settleWithinBudget,
  startForceLayout,
} from "../src/features/journal/lib/graph-layout.ts";
import {
  precomputeLayout,
  precomputedPositions,
  preserveHeldNodes,
} from "../src/features/journal/lib/graph-precompute.ts";
import {
  GRAPH_TOKENS,
  LEGEND_NODE_ROWS,
  NODE_RADIUS,
  anchorRadius,
  nodeColorName,
  resolvePalette,
} from "../src/features/journal/lib/graph-palette.ts";
import {
  UNSORTED_CLUSTER_ID,
  anchorLinkEdges,
  anchorLinks,
  anchorSetField,
  buildClusters,
  clusterLabel,
  clusterSubField,
  composeClusterView,
  projectedLinks,
  visibleNodes,
} from "../src/features/journal/lib/graph-cluster.ts";
import {
  HUB_DEGREE_FLOOR,
  LABEL_SCALE_FLOOR,
  LABEL_SCALE_NEAR,
  MAX_LABEL_CHARS,
  drawScene,
  truncateLabel,
} from "../src/features/journal/lib/graph-paint.ts";
import {
  asGraphNodeKind,
  graphNodeStates,
  isolatedInViewNodes,
  nodeKey,
  normalizeGraphField,
} from "../src/features/journal/lib/graph-payload.ts";
import { graphPath, graphQueryParams } from "../src/features/journal/lib/graph-query.ts";
import {
  clampScale,
  fitView,
  hitRadiusFor,
  nextNodeIndex,
  pickNode,
  zoomAt,
} from "../src/features/journal/lib/graph-view.ts";
import type {
  GraphEdge,
  GraphField,
  GraphNode,
  GraphPosition,
  GraphViewFilters,
} from "../src/features/journal/types.ts";

const sliceFile = (path: string) =>
  readFileSync(new URL(`../src/features/journal/${path}`, import.meta.url), "utf8");

// Strings that exist only in the graph island. A copy change moves them; the barrel rule below
// names the ones it measured so it cannot silently measure nothing.
const ISLAND_MARKERS = ["Graph map:", "Nodes in this view", "live-simulation ceiling"];

function filesUnder(dir: string, match: (name: string) => boolean): string[] {
  if (!existsSync(dir)) {
    return [];
  }
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      found.push(...filesUnder(path, match));
    } else if (match(entry.name)) {
      found.push(path);
    }
  }
  return found;
}

const jiti = createJiti(import.meta.url, {
  jsx: { runtime: "automatic", importSource: "react" },
  alias: {
    "@/shared/assets/zerlings": new URL("./fixtures/stub-zerlings.ts", import.meta.url).href,
    "@": new URL("../src/", import.meta.url).href,
  },
});
const { GraphLegend } = await jiti.import(
  "../src/features/journal/components/graph-legend.tsx",
);
const { GraphNodeList } = await jiti.import(
  "../src/features/journal/components/graph-node-list.tsx",
);
const { GraphNodePanel } = await jiti.import(
  "../src/features/journal/components/graph-node-panel.tsx",
);
const { GraphCanvas } = await jiti.import(
  "../src/features/journal/components/graph-canvas.tsx",
);
const { graphHrefFrom, noteHrefFromGraph, streamHrefFrom } = await jiti.import(
  "../src/features/journal/lib/note-rails.ts",
);

const noop = () => {};

// The files this task shipped, named once so every sweep below says what it measured — the same
// ledger discipline `journal-note-view.test.mts` uses for T8's slice.
const GRAPH_SLICE = [
  "components/graph-canvas.tsx",
  "components/graph-filters.tsx",
  "components/graph-legend.tsx",
  "components/graph-node-list.tsx",
  "components/graph-node-panel.tsx",
  "lib/graph-cluster.ts",
  "lib/graph-copy.ts",
  "lib/graph-filters.ts",
  "lib/graph-layout.ts",
  "lib/graph-paint.ts",
  "lib/graph-palette.ts",
  "lib/graph-payload.ts",
  "lib/graph-query.ts",
  "lib/graph-view.ts",
  "lib/use-graph.ts",
] as const;

const NOTE_ID = "11111111-1111-4111-8111-111111111111";
const IDEA_ID = "22222222-2222-4222-8222-222222222222";
const PROJECT_ID = "33333333-3333-4333-8333-333333333333";
const FEATURE_ID = "44444444-4444-4444-8444-444444444444";
const TASK_ID = "55555555-5555-4555-8555-555555555555";

const node = (
  kind: string,
  id: string,
  label: string,
  extra: Partial<GraphNode> = {},
): GraphNode => ({
  kind: kind as GraphNode["kind"],
  id,
  label,
  projectId: PROJECT_ID,
  entryType: kind === "entry" ? "journal" : null,
  featureStatus: null,
  orphan: false,
  ...extra,
});

const edge = (fromKind: string, fromId: string, toKind: string, toId: string): GraphEdge => ({
  fromKind: fromKind as GraphEdge["fromKind"],
  fromId,
  toKind: toKind as GraphEdge["toKind"],
  toId,
  hasManual: false,
});

const fieldOf = (nodes: GraphNode[], edges: GraphEdge[]): GraphField => ({
  nodes,
  edges,
  positions: [],
  counts: {
    nodes: nodes.length,
    edges: edges.length,
    orphans: nodes.filter((n) => n.orphan).length,
    entryNodes: nodes.filter((n) => n.kind === "entry").length,
    projectNodes: nodes.filter((n) => n.kind === "project").length,
    featureNodes: nodes.filter((n) => n.kind === "feature").length,
    isolatedInView: isolatedInViewNodes(nodes, edges),
  },
  unsupported: { nodes: 0, edges: 0 },
});

const SMALL_FIELD = fieldOf(
  [
    node("entry", NOTE_ID, "Standing note"),
    node("entry", IDEA_ID, "An idea", { entryType: "idea" }),
    node("project", PROJECT_ID, "Overmind", { entryType: null }),
    node("feature", FEATURE_ID, "Journal graph", {
      entryType: null,
      featureStatus: "inProgress",
    }),
    node("entry", "66666666-6666-4666-8666-666666666666", "Loose thought", { orphan: true }),
  ],
  [edge("entry", NOTE_ID, "project", PROJECT_ID), edge("entry", NOTE_ID, "feature", FEATURE_ID)],
);

// A synthetic field parked in tier 2: a chain, so the settle has links to obey, in enough count
// to cross the 1000-node rung, with one server pin that has to survive the settle.
const SYNTHETIC_PIN_ID = "a0700";

const syntheticField = (nodeCount: number, positions: GraphPosition[] = []): GraphField => {
  const nodes: GraphNode[] = [];
  const edges: GraphEdge[] = [];
  const id = (index: number) => `a${index.toString().padStart(4, "0")}`;
  for (let index = 0; index < nodeCount; index += 1) {
    nodes.push(node("entry", id(index), `Note ${index}`));
    if (index > 0) {
      edges.push(edge("entry", id(index - 1), "entry", id(index)));
    }
  }
  return { ...fieldOf(nodes, edges), positions };
};

// ── The two vocabularies stay apart ────────────────────────────────────────────────────

test("the node union is three members and cannot express task", () => {
  const types = sliceFile("types.ts");
  assert.match(types, /export type GraphNodeKind = "entry" \| "project" \| "feature";/);
  assert.match(types, /export type JournalLinkKind = "entry" \| "feature" \| "project" \| "task";/);
  assert.ok(
    !/export type GraphNodeKind[^;]*task/.test(types),
    "task crept into the node vocabulary",
  );
});

test("a task node is refused at the door and counted, not drawn", () => {
  assert.equal(asGraphNodeKind("task"), null);
  assert.equal(asGraphNodeKind("entry"), "entry");
  assert.equal(asGraphNodeKind("Entry"), null, "the wire is camelCase, not PascalCase");

  const field = normalizeGraphField({
    nodes: [
      { kind: "entry", id: NOTE_ID, label: "Note", projectId: PROJECT_ID, orphan: false },
      { kind: "task", id: TASK_ID, label: "A task node", projectId: PROJECT_ID, orphan: false },
    ],
    edges: [
      { fromKind: "entry", fromId: NOTE_ID, toKind: "task", toId: TASK_ID, hasManual: true },
    ],
    positions: [],
    counts: { nodes: 1, edges: 0, orphans: 0, entryNodes: 1, projectNodes: 0, featureNodes: 0, isolatedInView: 0 },
  });

  assert.equal(field.nodes.length, 1, "a node kind this build cannot draw reached the canvas");
  assert.equal(field.unsupported.nodes, 1);
  assert.equal(field.edges.length, 0, "an edge into a refused node survived it");
  assert.equal(field.unsupported.edges, 1);
  assert.match(unsupportedRowsNotice(field.unsupported.nodes, field.unsupported.edges), /1 node row/);
});

test("no graph file reuses the rail kind union as a node kind", () => {
  for (const file of GRAPH_SLICE) {
    const source = sliceFile(file);
    assert.ok(
      !/:\s*JournalLinkKind\b/.test(source) && !/<JournalLinkKind>/.test(source),
      `${file} types a graph node with the four-member rail union`,
    );
  }
});

test("the camelCase wire posture is stated in code, not left to be discovered", () => {
  const payload = normalizeGraphField({
    nodes: [
      { kind: "feature", id: FEATURE_ID, label: "F", projectId: PROJECT_ID, featureStatus: "inProgress" },
      { kind: "entry", id: IDEA_ID, label: "I", projectId: PROJECT_ID, entryType: "idea" },
    ],
    edges: [],
    positions: [],
    counts: {},
  });
  assert.equal(payload.nodes[0].featureStatus, "inProgress");
  assert.equal(payload.nodes[1].entryType, "idea");
  assert.match(sliceFile("types.ts"), /JsonStringEnumConverter\(JsonNamingPolicy\.CamelCase\)/);
});

// ── isolatedInView is a node count, in node wording, and those nodes are drawn ──────────

test("one node with two off-view links reads 1, never 2", () => {
  const wire = {
    nodes: [{ kind: "entry", id: NOTE_ID, label: "Linked elsewhere", projectId: PROJECT_ID, orphan: false }],
    edges: [],
    positions: [],
    // The endpoint dropped both links (a Done feature and a task): the payload carries the
    // NODE flag only, and the tally it cannot carry is the one the legend must not imply.
    counts: { nodes: 1, edges: 0, orphans: 0, entryNodes: 1, projectNodes: 0, featureNodes: 0, isolatedInView: 1 },
  };
  const field = normalizeGraphField(wire);
  assert.equal(field.counts.isolatedInView, 1);
  assert.equal(isolatedInViewNodes(field.nodes, field.edges), field.counts.isolatedInView);

  const html = renderToStaticMarkup(React.createElement(GraphLegend, { field }));
  assert.match(html, /1 entry here links to something outside this view/);
  assert.ok(!/links lead outside/.test(html), "the legend counted links it was never given");
  assert.ok(!/2/.test(isolatedInViewLabel(1)));
});

test("the client derivation and the server count agree on a mixed field", () => {
  const states = graphNodeStates(SMALL_FIELD.nodes, SMALL_FIELD.edges);
  assert.equal(states.get(nodeKey("entry", "66666666-6666-4666-8666-666666666666")), "orphan");
  assert.equal(states.get(nodeKey("entry", NOTE_ID)), "linked");
  // The idea carries no edge in this payload and is not flagged orphan, so every link it has
  // leaves the view — the third state the legend explains and the painter draws.
  assert.equal(states.get(nodeKey("entry", IDEA_ID)), "offView");

  const lone = fieldOf(
    [
      node("entry", NOTE_ID, "Note", { orphan: false }),
      node("project", PROJECT_ID, "Project", { entryType: null, orphan: false }),
      node("feature", FEATURE_ID, "Feature", { entryType: null, orphan: true }),
    ],
    [edge("entry", NOTE_ID, "project", PROJECT_ID)],
  );
  assert.equal(lone.counts.isolatedInView, 0, "both rendered nodes carry the one edge");
  assert.equal(isolatedInViewNodes(lone.nodes, lone.edges), lone.counts.isolatedInView);
});

test("orphanLabel counts nodes and the aria label carries the real numbers", () => {
  assert.equal(orphanLabel(1), "1 node has no links at all");
  assert.equal(orphanLabel(7), "7 nodes have no links at all");
  const label = graphAriaLabel(SMALL_FIELD.counts);
  assert.match(label, /5 nodes/);
  assert.match(label, /2 links/);
  assert.match(label, /1 nodes carry no links at all|1 node has no links/, "orphan count missing");
  assert.match(label, /list below/);
});

// ── URL-backed filters ────────────────────────────────────────────────────────────────

const filterWith = (patch: Partial<GraphViewFilters>): GraphViewFilters => ({
  ...EMPTY_GRAPH_FILTERS,
  ...patch,
});

test("filters round-trip through the URL and survive a reload", () => {
  const filters = filterWith({
    types: ["decision", "idea"],
    project: PROJECT_ID,
    focus: `entry:${NOTE_ID}`,
    depth: "2",
    orphansOnly: false,
    includeDone: true,
    search: "A note",
  });
  const target = writeGraphFilters(new URLSearchParams(), filters);
  assert.deepEqual(parseGraphFilters(target), filters);
});

test("the empty filter set writes no parameters at all", () => {
  const target = writeGraphFilters(new URLSearchParams(), EMPTY_GRAPH_FILTERS);
  assert.equal(target.toString(), "");
  assert.equal(isGraphFilterActive(EMPTY_GRAPH_FILTERS), false);
  assert.equal(isGraphFilterActive(filterWith({ includeDone: true })), true);
});

test("a hand-typed URL degrades instead of asking the server for a 400", () => {
  const parsed = parseGraphFilters(new URLSearchParams("type=journal,audit,DECISION&depth=3"));
  assert.deepEqual(parsed.types, ["journal", "decision"], "unknown types reached the request");
  assert.equal(parsed.focus, "", "depth alone is DepthWithoutFocus, so it drops with it");

  const badFocus = parseGraphFilters(new URLSearchParams("focus=task%3A" + TASK_ID));
  assert.equal(badFocus.focus, "", "a task focus is refused by the endpoint, so never ask");
});

test("orphans-only and a focus never ship together", () => {
  const focused = filterWith({ focus: `entry:${NOTE_ID}`, depth: "2" });
  const orphaned = withOrphansOnly(focused, true);
  assert.equal(orphaned.orphansOnly, true);
  assert.equal(orphaned.focus, "");
  assert.equal(orphaned.depth, "");

  const refocused = withFocus(filterWith({ orphansOnly: true }), `project:${PROJECT_ID}`);
  assert.equal(refocused.focus, `project:${PROJECT_ID}`);
  assert.equal(refocused.orphansOnly, false);
});

test("the toggle keeps the chip order the server's own list uses", () => {
  assert.deepEqual(toggleGraphEntryType([], "idea"), ["idea"]);
  assert.deepEqual(toggleGraphEntryType(["idea", "journal"], "idea"), ["journal"]);
  assert.deepEqual(toggleGraphEntryType(GRAPH_ENTRY_TYPES, "journal"), ["decision", "idea"]);
});

test("the request sends exactly the parameters the endpoint binds", () => {
  const api = readFileSync(
    new URL("../../src/Overmind.Host/Api/GraphApi.cs", import.meta.url),
    "utf8",
  );
  const handler = /Task<IResult> ReadGraph\(([\s\S]*?)OvermindDbContext db,/.exec(api);
  assert.ok(handler, "ReadGraph signature not found — the pin below measured nothing");
  const bound = handler[1];
  for (const name of GRAPH_QUERY_KEYS) {
    assert.match(bound, new RegExp(`string\\? ${name},|string\\[\\]\\? ${name},`), `${name} is not a bound parameter`);
  }

  const params = graphQueryParams(
    filterWith({ types: ["journal"], project: PROJECT_ID, focus: `entry:${NOTE_ID}`, depth: "2", orphansOnly: true, includeDone: true, search: "x" }),
  );
  for (const name of GRAPH_QUERY_KEYS) {
    assert.ok(params.has(name), `${name} never leaves the client`);
  }
  assert.equal(params.get("type"), "journal");
  assert.equal(params.get("focus"), `entry:${NOTE_ID}`);

  assert.equal(graphPath(EMPTY_GRAPH_FILTERS), "/graph");
  assert.match(graphPath(filterWith({ includeDone: true })), /^\/graph\?includeDone=true$/);
});

// ── The palette is tokens or nothing ──────────────────────────────────────────────────

test("every graph token name is declared in the shipped theme", () => {
  const globals = readFileSync(new URL("../src/app/globals.css", import.meta.url), "utf8");
  for (const name of Object.values(GRAPH_TOKENS)) {
    assert.match(globals, new RegExp(`${name}:`), `${name} is not a shipped token`);
  }
});

test("resolvePalette follows var() chains and reports what it cannot resolve", () => {
  const table: Record<string, string> = {
    "--color-type-note": "#60a5fa",
    "--color-ceremony-goal": "var(--color-success)",
    "--color-success": "#22c55e",
    "--color-primary": "#a855f7",
    "--color-background": "#0a0a0f",
    "--color-foreground": "#e4e4e7",
    "--color-muted": "#8b8b96",
    "--color-type-decision": "#f59e0b",
    "--color-type-idea": "#facc15",
  };
  const resolved = resolvePalette((name) => table[name] ?? "");
  assert.equal(resolved.colors.feature, "#22c55e", "the var() chain did not resolve");
  assert.deepEqual(resolved.unresolved, []);

  const broken = resolvePalette((name) => (name === "--color-type-note" ? "" : table[name] ?? ""));
  assert.deepEqual(broken.unresolved, ["note"]);
});

test("a var() cycle is cut off instead of hanging the frame", () => {
  // Probed on the canvas slot: since R3's amendment the edge token is shared with the dashed
  // ring, so cycling it would (correctly) report two unresolved roles and hide what broke.
  const looped = resolvePalette((name) => {
    if (name === GRAPH_TOKENS.canvas) return "var(--color-a)";
    if (name === "--color-a") return "var(--color-b)";
    if (name === "--color-b") return "var(--color-a)";
    return "#202028";
  });
  assert.deepEqual(looped.unresolved, ["canvas"], "the cycle hung or silently painted");
});

test("node colour is type for entries and kind for the rest", () => {
  assert.equal(nodeColorName(node("entry", NOTE_ID, "N")), "note");
  assert.equal(nodeColorName(node("entry", IDEA_ID, "I", { entryType: "idea" })), "idea");
  assert.equal(nodeColorName(node("entry", IDEA_ID, "D", { entryType: "decision" })), "decision");
  assert.equal(nodeColorName(node("project", PROJECT_ID, "P", { entryType: null })), "project");
  assert.equal(nodeColorName(node("feature", FEATURE_ID, "F", { entryType: null })), "feature");
  assert.deepEqual(
    LEGEND_NODE_ROWS.map((row) => row.color),
    ["note", "decision", "idea", "project", "feature"],
  );
});

// ── Layout: determinism, the pin surface, the ceiling ─────────────────────────────────

test("the seed layout is the same map twice, and pins win", () => {
  const nodes = SMALL_FIELD.nodes;
  const first = seedPositions(nodes, [], 800, 600);
  const second = seedPositions(nodes, [], 800, 600);
  assert.deepEqual(
    first.map((n) => [Math.round(n.x), Math.round(n.y)]),
    second.map((n) => [Math.round(n.x), Math.round(n.y)]),
  );

  const pinned = seedPositions(nodes, [{ kind: "entry", id: NOTE_ID, x: 12, y: 34 }], 800, 600);
  const pinnedNode = pinned.find((n) => n.id === NOTE_ID);
  assert.equal(pinnedNode?.x, 12);
  assert.equal(pinnedNode?.y, 34);

  const unpinned = seedPositions(nodes, [], 800, 600);
  const moved = pinned.filter((n, index) => Math.round(n.x) !== Math.round(unpinned[index]!.x));
  assert.equal(moved.length, 1, "pinning one node moved the others too");
});

test("the force layout moves nodes off the seed and stays finite", () => {
  const nodes = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  const links = seedLayoutLinks(SMALL_FIELD.edges, nodes);
  assert.equal(links.length, SMALL_FIELD.edges.length);
  const before = nodes.map((n) => n.x);

  settleLayout(nodes, links, { width: 800, height: 600 }, 60);

  assert.ok(nodes.every((n) => Number.isFinite(n.x) && Number.isFinite(n.y)));
  assert.ok(nodes.some((n, i) => Math.abs(n.x - before[i]!) > 0.5), "the layout never moved");
});

test("a pinned node holds its place through a tick", () => {
  const nodes = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  const links = seedLayoutLinks(SMALL_FIELD.edges, nodes);
  const target = nodes.find((n) => n.id === NOTE_ID)!;
  pinNode(target, 111, 222);

  const handle = startForceLayout(nodes, links, { width: 800, height: 600 });
  for (let tick = 0; tick < 30; tick += 1) {
    handle.simulation.tick();
  }
  handle.stop();

  assert.equal(target.x, 111);
  assert.equal(target.y, 222);

  releasePinnedNodes(nodes);
  assert.equal(target.fx, null);
});

test("the synchronous settle is bounded by node count, not left to block the tab", () => {
  // Measured on this box: one d3 tick over ~999 nodes costs ~11 ms, so an unbounded 300-tick
  // convergence would freeze the main thread for about 1.8 s — under reduced motion, where the
  // owner asked for no animation, not for a stall.
  assert.equal(settleTicksFor(50), 300);
  assert.equal(settleTicksFor(300), 100);
  assert.equal(settleTicksFor(999), 20);
  const many = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  const before = many.map((n) => [Math.round(n.x), Math.round(n.y)]);
  settleWithinBudget(many, [], { width: 800, height: 600 });
  assert.ok(many.every((n) => Number.isFinite(n.x) && Number.isFinite(n.y)));
  assert.equal(many.length, SMALL_FIELD.nodes.length);
  assert.ok(before.length === many.length);
});

test("the ceiling ladder flips at 1000 and 5000", () => {
  assert.equal(SIM_CEILING, 1000);
  assert.equal(PRECOMPUTE_CEILING, 5000);
  assert.equal(layoutModeFor(1000), "simulate");
  assert.equal(layoutModeFor(1001), "precomputed");
  assert.equal(layoutModeFor(5000), "precomputed");
  assert.equal(layoutModeFor(5001), "clustered");
  assert.equal(isOverCeiling(1200), true);
  assert.match(
    precomputedNotice(1204, SIM_CEILING),
    /1204 nodes are above the 1000-node live-simulation ceiling/,
  );
  assert.match(
    clusteredNotice(5001, PRECOMPUTE_CEILING),
    /5001 nodes are above the 5000-node precompute ceiling/,
  );
  assert.match(ceilingMessage(1204, SIM_CEILING), /1204 nodes are above the 1000-node/);
});

test("the tier-2 precompute settles the same map twice, pins win, every coordinate finite", async () => {
  const field = syntheticField(1500, [{ kind: "entry", id: SYNTHETIC_PIN_ID, x: 77, y: 88 }]);
  assert.equal(layoutModeFor(field.nodes.length), "precomputed", "the fixture left tier 2");

  const first = await precomputeLayout(field, { width: 1200, height: 900 });
  const second = await precomputeLayout(field, { width: 1200, height: 900 });
  const frame = (nodes: LayoutNode[]) => nodes.map((n) => [Math.round(n.x), Math.round(n.y)]);
  assert.deepEqual(frame(first), frame(second), "the same field settled into a different map");

  const pinned = first.find((n) => n.id === SYNTHETIC_PIN_ID);
  assert.equal(pinned?.x, 77);
  assert.equal(pinned?.y, 88);
  assert.ok(first.every((n) => Number.isFinite(n.x) && Number.isFinite(n.y)));
});

test("the slice transport lands on the frame the worker's one-shot settle produces", async () => {
  // The shipped transport is the chunked slices; precomputedPositions is verbatim what the
  // worker entry calls once the bundler flip lands. This pair is the guard that the flip
  // cannot change what the owner sees — same field, same frame, whichever transport runs it.
  const field = syntheticField(1500, [{ kind: "entry", id: SYNTHETIC_PIN_ID, x: 77, y: 88 }]);
  const workerFrame = precomputedPositions({
    nodes: field.nodes,
    edges: field.edges,
    positions: field.positions,
    width: 1200,
    height: 900,
  });
  const fallbackFrame = await precomputeLayout(field, { width: 1200, height: 900 });
  const frame = (nodes: LayoutNode[]) => nodes.map((n) => [Math.round(n.x), Math.round(n.y)]);
  assert.deepEqual(frame(fallbackFrame), frame(workerFrame), "the transports disagreed");
  const pinned = fallbackFrame.find((n) => n.id === SYNTHETIC_PIN_ID);
  assert.equal(pinned?.x, 77);
  assert.equal(pinned?.y, 88);
});

test("the worker entry calls the lib and carries no layout math of its own", () => {
  const source = sliceFile("lib/graph-layout.worker.ts");
  assert.match(source, /precomputedPositions/);
  assert.ok(!/d3-force/.test(source), "d3 crept into the worker entry");
  assert.ok(
    !/Math\.(sqrt|cos|sin|pow|atan2|random)/.test(source),
    "trigonometry crept into the worker entry",
  );
});

test("neighbourhoods are built from links, not rescanned per frame", () => {
  const nodes = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  const links = seedLayoutLinks(SMALL_FIELD.edges, nodes);
  const map = buildNeighbourhoods(links);
  const note = nodeKey("entry", NOTE_ID);
  assert.ok(map.get(note)!.keys.has(nodeKey("project", PROJECT_ID)));
  assert.ok(map.get(note)!.keys.has(nodeKey("feature", FEATURE_ID)));
  assert.equal(map.get(nodeKey("entry", "66666666-6666-4666-8666-666666666666")), undefined);
});

// ── View math and picking ─────────────────────────────────────────────────────────────

test("fit frames the field and zoom keeps its anchor", () => {
  const points = [
    { x: 0, y: 0 },
    { x: 400, y: 200 },
  ];
  const view = fitView(points, 800, 600, 40);
  assert.ok(view.scale > 0.5 && view.scale <= 2.2, `scale ${view.scale}`);
  const left = view.tx;
  const zoomed = zoomAt(view, 2, 400, 300);
  const anchorWorld = (400 - left) / view.scale;
  const anchorBack = anchorWorld * zoomed.scale + zoomed.tx;
  assert.ok(Math.abs(anchorBack - 400) < 1e-6, "the point under the cursor slid");
  assert.ok(zoomed.scale > view.scale);
});

test("fit on an empty or broken field is the identity, not NaN", () => {
  assert.deepEqual(fitView([], 800, 600), { scale: 1, tx: 0, ty: 0 });
  assert.deepEqual(fitView([{ x: Number.NaN, y: 0 }], 800, 600), { scale: 1, tx: 0, ty: 0 });
  assert.equal(clampScale(Number.NaN), 0.2);
});

test("picking keeps a 24px target floor at any zoom", () => {
  const points = [
    { key: "a", x: 100, y: 100, radius: 6 },
    { key: "b", x: 140, y: 100, radius: 9 },
  ];
  assert.equal(pickNode(points, 100, 100, 1), "a");
  assert.equal(pickNode(points, 139, 100, 1), "b");
  assert.equal(pickNode(points, 400, 400, 1), null);
  assert.ok(hitRadiusFor(6, 0.25) >= 12, "a zoomed-out dot became unclickable");
});

test("arrow keys walk every node and wrap", () => {
  assert.equal(nextNodeIndex(null, 1, 5), 0);
  assert.equal(nextNodeIndex(4, 1, 5), 0);
  assert.equal(nextNodeIndex(0, -1, 5), 4);
  assert.equal(nextNodeIndex(2, 1, 0), null);
});

// ── The painter: tokens only, three states drawn, hover dims ──────────────────────────

interface PaintCall {
  op: string;
  fillStyle?: string;
  strokeStyle?: string;
  globalAlpha?: number;
  dash?: number[];
  // The tier-3 pins read label text off the frame — "honestly labeled Name (N)" is a claim
  // about the string, not about the count of strings.
  text?: string;
}

function recorder(): { calls: PaintCall[]; target: Record<string, unknown> } {
  const calls: PaintCall[] = [];
  const state: { fillStyle: string; strokeStyle: string; globalAlpha: number; lineWidth: number; font: string; dash: number[] } = {
    fillStyle: "",
    strokeStyle: "",
    globalAlpha: 1,
    lineWidth: 1,
    font: "",
    dash: [],
  };
  const mark = (op: string) => {
    calls.push({
      op,
      fillStyle: state.fillStyle,
      strokeStyle: state.strokeStyle,
      globalAlpha: state.globalAlpha,
      dash: [...state.dash],
    });
  };
  const target: Record<string, unknown> = {
    get fillStyle() {
      return state.fillStyle;
    },
    set fillStyle(value: string) {
      state.fillStyle = value;
    },
    get strokeStyle() {
      return state.strokeStyle;
    },
    set strokeStyle(value: string) {
      state.strokeStyle = value;
    },
    get globalAlpha() {
      return state.globalAlpha;
    },
    set globalAlpha(value: number) {
      state.globalAlpha = value;
    },
    lineWidth: 1,
    font: "",
    clearRect: () => mark("clearRect"),
    save: () => mark("save"),
    restore: () => mark("restore"),
    beginPath: () => mark("beginPath"),
    closePath: () => mark("closePath"),
    moveTo: () => {},
    lineTo: () => {},
    arc: () => mark("arc"),
    rect: () => mark("rect"),
    fill: () => mark("fill"),
    stroke: () => mark("stroke"),
    fillText: (text: string) => {
      mark("fillText");
      calls[calls.length - 1]!.text = text;
    },
    setLineDash: (segments: number[]) => {
      state.dash = segments;
      mark("setLineDash");
    },
  };
  return { calls, target };
}

const PALETTE_TABLE: Record<string, string> = {
  "--color-type-note": "#60a5fa",
  "--color-type-decision": "#f59e0b",
  "--color-type-idea": "#facc15",
  "--color-primary": "#a855f7",
  "--color-ceremony-goal": "var(--color-success)",
  "--color-success": "#22c55e",
  "--color-background": "#0a0a0f",
  "--color-foreground": "#e4e4e7",
  "--color-muted": "#8b8b96",
};

const paletteColors = resolvePalette((name) => PALETTE_TABLE[name] ?? "").colors;

function sceneOf(patch: Partial<Record<string, unknown>> = {}) {
  const nodes = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  const links = seedLayoutLinks(SMALL_FIELD.edges, nodes);
  return {
    nodes,
    links,
    scene: {
      nodes,
      links,
      view: { scale: 1, tx: 0, ty: 0 },
      colors: paletteColors,
      states: graphNodeStates(SMALL_FIELD.nodes, SMALL_FIELD.edges),
      hoverKey: null as string | null,
      selectedKey: null as string | null,
      neighbours: null as Set<string> | null,
      width: 800,
      height: 600,
      font: "12px ui-sans-serif, system-ui, sans-serif",
      tier: "simulate",
      ...patch,
    },
  };
}

test("every colour the painter uses is a palette token value", () => {
  const allowed = new Set<string>(Object.values(paletteColors));
  const { target, calls } = recorder();
  const { scene } = sceneOf();
  drawScene(target as never, scene as never);

  const used = calls.filter((call) => call.op === "fill" || call.op === "stroke" || call.op === "fillText");
  assert.ok(used.length > 0, "the painter drew nothing — this assert measured no colours");
  for (const call of used) {
    for (const style of [call.fillStyle, call.strokeStyle]) {
      if (style === undefined || style.length === 0) {
        continue;
      }
      assert.ok(allowed.has(style), `a paint call wore ${style}, which is not a token value`);
    }
  }
});

test("hover dims the rest of the field to 30 percent", () => {
  const { target, calls } = recorder();
  const { nodes, scene } = sceneOf();
  const hoverKey = nodeKey(nodes[0]!.kind, nodes[0]!.id);
  const neighbours = buildNeighbourhoods(scene.links).get(hoverKey);
  drawScene(target as never, {
    ...scene,
    hoverKey,
    neighbours: neighbours?.keys ?? new Set<string>(),
  } as never);

  const farNode = nodes[nodes.length - 1]!;
  const touched = calls.filter((call) => call.op === "fill" || call.op === "stroke");
  assert.ok(touched.some((call) => call.globalAlpha === 0.3), "nothing dimmed to 30%");
  assert.ok(touched.some((call) => call.globalAlpha === 1), "the hovered neighbourhood dimmed too");
  assert.equal(
    scene.states.get(nodeKey(farNode.kind, farNode.id)),
    "orphan",
    "the fixture's far node stopped being the dim one",
  );
});

test("the three node states are all drawn", () => {
  const { target, calls } = recorder();
  const { scene, nodes } = sceneOf();
  drawScene(target as never, scene as never);

  const dashed = calls.filter((call) => call.dash.length > 0 && call.op === "stroke");
  assert.ok(dashed.length > 0, "an off-view node was drawn with no dashed ring");

  const offViewNode = nodes.find((n) => n.id === IDEA_ID)!;
  assert.equal(scene.states.get(nodeKey(offViewNode.kind, offViewNode.id)), "offView");

  const orphanFills = calls.filter((call) => call.op === "fill" && call.globalAlpha === 1);
  assert.ok(orphanFills.length > 0, "linked nodes stopped being painted");
  const anyHollow = calls.some(
    (call) => call.op === "stroke" && call.globalAlpha !== 1 && call.globalAlpha !== undefined,
  );
  assert.ok(anyHollow, "the orphan shell lost its distinct alpha");
});

test("the edge ladder is two named tokens, not two shades everyone agreed on", () => {
  assert.equal(GRAPH_TOKENS.edge, "--color-muted", "R3's amended resting edge token moved");
  assert.equal(GRAPH_TOKENS.edgeStrong, "--color-foreground", "R3's amended incident edge token moved");

  const ladder = resolvePalette((name) => `painted(${name})`).colors;
  assert.notEqual(ladder.edge, ladder.edgeStrong, "the ladder collapsed onto one rung");

  const { nodes, scene } = sceneOf({ colors: ladder });
  const project = nodes.find((n) => n.id === PROJECT_ID)!;
  const projectKey = nodeKey(project.kind, project.id);
  const edge = (call: PaintCall) =>
    call.op === "stroke" &&
    call.dash.length === 0 &&
    (call.strokeStyle === ladder.edge || call.strokeStyle === ladder.edgeStrong);

  const hover = recorder();
  drawScene(
    hover.target as never,
    { ...scene, hoverKey: projectKey, neighbours: new Set([projectKey]) } as never,
  );
  const hoveredEdges = hover.calls.filter(edge);
  assert.equal(hoveredEdges.length, scene.links.length, "a link painted no edge — this run measured nothing");
  assert.equal(
    hoveredEdges.filter((call) => call.strokeStyle === ladder.edgeStrong).length,
    1,
    "hovering one endpoint did not lift exactly the incident link",
  );
  assert.equal(
    hoveredEdges.filter((call) => call.strokeStyle === ladder.edge).length,
    scene.links.length - 1,
    "the resting links left the base rung while nothing was picked",
  );

  const selection = recorder();
  drawScene(
    selection.target as never,
    { ...scene, hoverKey: null, selectedKey: projectKey } as never,
  );
  const selectedEdges = selection.calls.filter(edge);
  assert.equal(
    selectedEdges.filter((call) => call.strokeStyle === ladder.edgeStrong).length,
    1,
    "a picked node no longer strengthens its own links",
  );
  assert.ok(
    selectedEdges.every((call) => call.globalAlpha === 1),
    "selection alone faded the field — the 30% dim is a hover act, not a click act",
  );
});

test("the selection ring wears the primary token at 40 percent", () => {
  const { target, calls } = recorder();
  const { nodes, scene } = sceneOf();
  const key = nodeKey(nodes[0]!.kind, nodes[0]!.id);
  drawScene(target as never, { ...scene, selectedKey: key } as never);

  const rings = calls.filter((call) => call.op === "stroke" && call.globalAlpha === 0.4);
  assert.equal(rings.length, 1, "the selection ring is not the one 40% stroke");
  assert.equal(rings[0]!.strokeStyle, paletteColors.selected);
});

test("labels are truncated and only appear where they read", () => {
  const long = "a".repeat(80);
  assert.equal(truncateLabel(long).length, 42);
  assert.equal(truncateLabel("  short  "), "short");

  const { target, calls } = recorder();
  const { scene } = sceneOf({ view: { scale: 0.3, tx: 0, ty: 0 } });
  drawScene(target as never, scene as never);
  assert.equal(calls.filter((call) => call.op === "fillText").length, 0, "zoomed-out labels sprayed");

  const zoomed = recorder();
  drawScene(zoomed.target as never, { ...scene, view: { scale: 1.4, tx: 0, ty: 0 } } as never);
  assert.ok(zoomed.calls.filter((call) => call.op === "fillText").length >= SMALL_FIELD.nodes.length);
});

test("the canvas element is an image with counts and a keyboard entry", () => {
  const html = renderToStaticMarkup(
    React.createElement(GraphCanvas, {
      field: SMALL_FIELD,
      selectedKey: null,
      reducedMotion: true,
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /role="img"/);
  assert.match(html, /aria-label="Graph map: 5 nodes/);
  assert.match(html, /tabindex="0"/, "the canvas is not reachable by keyboard");
  assert.match(html, /focus-visible:ring-2/);
  assert.match(html, /Reduced motion is on|reduced motion/i);
});

test("over the ceiling the island says the real count instead of animating", () => {
  const big = fieldOf(
    Array.from({ length: 1201 }, (_, index) =>
      node("entry", `n${index}`, `Note ${index}`, { orphan: index % 3 === 0 }),
    ),
    [],
  );
  const html = renderToStaticMarkup(
    React.createElement(GraphCanvas, {
      field: big,
      selectedKey: null,
      reducedMotion: false,
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /1201 nodes are above the 1000-node live-simulation ceiling/);
});

test("the list alternative covers every node and is the keyboard path", () => {
  const html = renderToStaticMarkup(
    React.createElement(GraphNodeList, {
      field: SMALL_FIELD,
      selectedKey: nodeKey("entry", NOTE_ID),
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /aria-label="Nodes in this view"/);
  const rows = html.match(/aria-pressed=/g) ?? [];
  assert.equal(rows.length, SMALL_FIELD.nodes.length, "a node dropped out of the list");
  assert.match(html, /aria-pressed="true"/);
  assert.match(html, /Standing note/);
  assert.match(html, /no links/);
});

test("the legend swatches and the painter share one token map", () => {
  const html = renderToStaticMarkup(React.createElement(GraphLegend, { field: SMALL_FIELD }));
  for (const row of LEGEND_NODE_ROWS) {
    assert.ok(
      html.includes(`var(${GRAPH_TOKENS[row.color]})`),
      `the legend lost the ${row.label} swatch`,
    );
  }
  assert.match(html, /2 links/);
});

test("the panel states the node, its own kind and never a route change", () => {
  const html = renderToStaticMarkup(
    React.createElement(GraphNodePanel, {
      field: SMALL_FIELD,
      node: SMALL_FIELD.nodes[2]!,
      focusedHandle: "",
      onClear: noop,
      onCentre: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /aria-label="Node context"/);
  assert.match(html, /Project/);
  assert.match(html, /1 link in this view/);
  assert.match(html, /Centre on this node/);
  // A project node states the address gap instead of guessing a URL it was never given.
  assert.match(html, /guid the graph stores/);
  assert.ok(!/href=/.test(html), "the panel rendered a link it cannot build honestly");
});

test("the note branch of the panel reads through the query layer", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const html = renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client },
      React.createElement(GraphNodePanel, {
        field: SMALL_FIELD,
        node: SMALL_FIELD.nodes[1]!,
        focusedHandle: `entry:${NOTE_ID}`,
        onClear: noop,
        onCentre: noop,
        onOpenNote: noop,
      }),
    ),
  );
  assert.match(html, /Idea/);
  assert.match(html, /links leave this view/);
  assert.match(html, /Loading the note/, "the note context did not go through React Query");
  assert.match(html, /Clear centre/);
});

test("empty-field copy names the term as typed and promises nothing about case", () => {
  assert.equal(emptyFieldMessage(false, ""), "No nodes match these filters. Widen them, or capture a note first.");
  const typed = emptyFieldMessage(true, "  Учет  ");
  assert.match(typed, /Учет/);
  assert.match(typed, /as typed/);
  assert.ok(!/case-insensitive|case insensitive|без учёта регистра|регистр не учитывается/i.test(typed));
  assert.ok(!/case-insensitive|без учёта регистра/i.test(GRAPH_SEARCH_HINT));
});

test("nothing on the graph surface advertises case-insensitive search", () => {
  const offenders: string[] = [];
  for (const file of GRAPH_SLICE) {
    const source = sliceFile(file);
    if (/case[- ]insensitive|insensitive to case|без[- ]учёта регистра|регистр не важен/i.test(source)) {
      offenders.push(file);
    }
  }
  assert.deepEqual(offenders, [], "a string promised a match the store does not make");
});

test("the note preview excerpt cuts on blocks, not mid-token", () => {
  assert.equal(noteExcerpt("one\n\ntwo"), "one\n\ntwo");
  const long = "x".repeat(500);
  assert.ok(noteExcerpt(long).length <= 401);
  assert.match(noteExcerpt(long), /…$/);
  assert.ok(Array.from(noteExcerpt("emoji 🚧 ".repeat(120))).length <= 401);
});

test("kinds and states read as words the legend also uses", () => {
  assert.equal(nodeKindLabel(node("entry", NOTE_ID, "N")), "Journal note");
  assert.equal(nodeKindLabel(node("entry", IDEA_ID, "I", { entryType: "idea" })), "Idea");
  assert.equal(nodeKindLabel(node("feature", FEATURE_ID, "F", { entryType: null })), "Feature");
  assert.equal(stateLabel("offView"), "links leave this view");
  assert.equal(nodeCountLabel({ ...SMALL_FIELD.counts, edges: 1 }).includes("1 link"), true);
});

// ── Conventions this route must not break ─────────────────────────────────────────────

test("double-click navigation returns to the graph, filters intact", () => {
  const href = noteHrefFromGraph(NOTE_ID, "?type=idea&orphansOnly=true");
  assert.match(href, /^\/journal\?note=/);
  assert.match(href, /from=journal-graph/);
  assert.equal(streamHrefFrom(href.split("?")[1] ?? ""), "/journal/graph?type=idea&orphansOnly=true");
  assert.equal(graphHrefFrom(""), "/journal/graph");
  assert.equal(streamHrefFrom(`note=${NOTE_ID}`), "/journal", "the inbox path kept its own back link");
});

test("the graph route lives in the pages layer and the island stays out of the barrel", () => {
  assert.ok(existsSync(new URL("../src/app/journal/graph/page.page.tsx", import.meta.url)));
  const route = readFileSync(new URL("../src/app/journal/graph/page.page.tsx", import.meta.url), "utf8");
  assert.ok(route.includes('from "@/pages/journal-graph-page"'), "the route bypasses the pages layer");
  assert.ok(!route.includes('from "@/features/'), "the route reaches into a feature directly");

  const barrel = readFileSync(new URL("../src/features/journal/index.ts", import.meta.url), "utf8")
    .split("\n")
    .filter((line) => !line.trimStart().startsWith("//"))
    .join("\n");
  for (const banned of ["graph-canvas", "graph-layout", "graph-paint", "components/graph"]) {
    assert.ok(
      !barrel.includes(banned),
      `the journal barrel re-exports ${banned} — every route that imports the barrel would then carry the island`,
    );
  }

  // The text list above only sees what the barrel NAMES. A re-export through an intermediate
  // module keeps the file clean and still pulls d3-force into every route, so the shipped
  // bundle is the second half of this rule — measured, not inferred.
  const outRoot = fileURLToPath(new URL("../out/", import.meta.url));
  const chunks = filesUnder(join(outRoot, "_next/static/chunks"), (name) => name.endsWith(".js"));
  const exported = filesUnder(outRoot, (name) => name.endsWith(".html"));
  assert.ok(
    chunks.length > 0 && exported.length > 1,
    `web/out carries ${chunks.length} chunk(s) and ${exported.length} route(s) — run \`npm run build\` in web/ before \`npm test\`; this half of the barrel rule measures the shipped bundle and cannot measure an absent one`,
  );

  for (const marker of ISLAND_MARKERS) {
    const island = chunks.filter((chunk) => readFileSync(chunk, "utf8").includes(marker));
    assert.ok(
      island.length > 0,
      `no chunk in web/out holds "${marker}" while web/out does exist — the export is stale; rebuild before trusting this rule`,
    );
    const names = island.map((chunk) => basename(chunk));
    const carriers = exported
      .filter((file) => {
        const html = readFileSync(file, "utf8");
        return names.some((name) => html.includes(name));
      })
      .map((file) => relative(outRoot, file).split("\\").join("/"));
    assert.deepEqual(
      carriers,
      ["journal/graph.html"],
      `"${marker}" is painted into ${carriers.join(", ") || "no route"} — the island reached a shared chunk, so every route importing the journal barrel now ships the simulation`,
    );
  }
});

test("the graph files hold no sub-24px pointer target and no xs control", () => {
  const offenders: string[] = [];
  for (const file of GRAPH_SLICE) {
    const source = sliceFile(file);
    if (/size="xs"/.test(source)) {
      offenders.push(`${file}: size="xs"`);
    }
    for (const match of source.matchAll(/<(button|input|select)\b[^>]*>/gs)) {
      const tag = match[0];
      if (/min-h-\[(1[0-9]|2[0-3])px\]|h-[23]\b|size-[12]\b/.test(tag)) {
        offenders.push(`${file}: ${tag.slice(0, 48)}`);
      }
    }
  }
  assert.deepEqual(offenders, [], "a new surface shipped a target under the 24px floor");
});

test("the graph files carry no useEffect data fetch", () => {
  const offenders: string[] = [];
  for (const file of GRAPH_SLICE) {
    const source = sliceFile(file);
    if (/useEffect\([^)]*fetch\(/s.test(source) || /apiGet\(/.test(source)) {
      offenders.push(file);
    }
  }
  assert.deepEqual(offenders, [], "a graph file reached the network outside the query layer");
});

test("the graph slice file names are the ones the sweeps above measure", () => {
  for (const file of GRAPH_SLICE) {
    assert.ok(sliceFile(file).length > 200, `${file} is missing, empty or wrong`);
  }
  assert.equal(GRAPH_SLICE.length, 15);
});

test("the hex sweep in journal-note-view covers exactly these files", () => {
  // R3 extends the EXISTING pin rather than adding a second one, so the pin's list and this
  // suite's ledger have to name the same files — otherwise the graph slice quietly leaves the
  // sweep while both suites stay green.
  const pinSource = readFileSync(new URL("./journal-note-view.test.mts", import.meta.url), "utf8");
  const block = /const T6_GRAPH_SLICE = \[([\s\S]*?)\] as const;/.exec(pinSource);
  assert.ok(block, "the hex pin lost its graph file list — the slice is no longer swept");
  const pinned = [...block[1].matchAll(/"([^"]+)"/g)].map((match) => match[1]);
  assert.deepEqual(pinned, [...GRAPH_SLICE]);
  assert.match(pinSource, /for \(const file of \[\.\.\.T8_SLICE, \.\.\.T6_GRAPH_SLICE\]\)/);
});

test("the frame clock and the pin surface keep the URL out of the click path", () => {
  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.ok(!/router|useRouter|window\.location/.test(canvas), "the canvas navigates — selection is state, not a route");
  assert.match(canvas, /onSelect\(pickAt\(x, y\)\)/, "a click no longer selects what is under it");
  assert.match(canvas, /onDoubleClick/, "double-click lost its door");
  assert.match(canvas, /node\.kind === "entry"/, "double-click no longer checks that a note is a note");
});

// ── Tier 2 on the canvas: seed first, swap-in, the label ladder ───────────────────────

const HUB_KEY = nodeKey("entry", "hub");
const LEAF_KEY = nodeKey("entry", "leaf-1");

// One hub over five leaves: at the mid band exactly one node clears the hub gate, so the
// recorder's fillText count names which rung opened without reading label text.
const HUB_FIELD = fieldOf(
  [
    node("entry", "hub", "Hub note"),
    node("entry", "leaf-1", "Leaf one"),
    node("entry", "leaf-2", "Leaf two"),
    node("entry", "leaf-3", "Leaf three"),
    node("entry", "leaf-4", "Leaf four"),
    node("entry", "leaf-5", "Leaf five"),
  ],
  [
    edge("entry", "hub", "entry", "leaf-1"),
    edge("entry", "hub", "entry", "leaf-2"),
    edge("entry", "hub", "entry", "leaf-3"),
    edge("entry", "hub", "entry", "leaf-4"),
    edge("entry", "hub", "entry", "leaf-5"),
  ],
);

function ladderScene(scale: number, patch: Record<string, unknown> = {}) {
  const nodes = seedPositions(HUB_FIELD.nodes, [], 800, 600);
  const links = seedLayoutLinks(HUB_FIELD.edges, nodes);
  return {
    scene: {
      nodes,
      links,
      view: { scale, tx: 0, ty: 0 },
      colors: paletteColors,
      states: graphNodeStates(HUB_FIELD.nodes, HUB_FIELD.edges),
      hoverKey: null as string | null,
      selectedKey: null as string | null,
      neighbours: null as Set<string> | null,
      width: 800,
      height: 600,
      font: "12px ui-sans-serif, system-ui, sans-serif",
      tier: "precomputed",
      ...patch,
    },
  };
}

const labelCount = (calls: PaintCall[]) => calls.filter((call) => call.op === "fillText").length;

test("the tier-2 label ladder: floor hides all but the focus, mid zoom lifts hubs, near zoom shows all", () => {
  assert.equal(HUB_DEGREE_FLOOR, 4);

  const far = recorder();
  drawScene(far.target as never, ladderScene(0.5).scene as never);
  assert.equal(labelCount(far.calls), 0, "a zoomed-out tier-2 field sprayed labels with nothing focused");

  const hovered = recorder();
  drawScene(hovered.target as never, ladderScene(0.5, { hoverKey: LEAF_KEY }).scene as never);
  assert.equal(labelCount(hovered.calls), 1, "the ladder dropped the hovered node's own label");
  const picked = recorder();
  drawScene(picked.target as never, ladderScene(0.5, { selectedKey: HUB_KEY }).scene as never);
  assert.equal(labelCount(picked.calls), 1, "the ladder dropped the selected node's own label");

  const mid = recorder();
  drawScene(mid.target as never, ladderScene(1.0).scene as never);
  assert.equal(labelCount(mid.calls), 1, "mid zoom must label exactly the one hub on this field");
  const midHover = recorder();
  drawScene(midHover.target as never, ladderScene(1.0, { hoverKey: LEAF_KEY }).scene as never);
  assert.equal(labelCount(midHover.calls), 2, "mid zoom dropped the hovered leaf next to its hub");

  const floorEdge = recorder();
  drawScene(floorEdge.target as never, ladderScene(LABEL_SCALE_FLOOR).scene as never);
  assert.equal(labelCount(floorEdge.calls), 1, "the hub band did not open at the floor it inherits");

  const nearEdge = recorder();
  drawScene(nearEdge.target as never, ladderScene(LABEL_SCALE_NEAR).scene as never);
  assert.equal(labelCount(nearEdge.calls), HUB_FIELD.nodes.length, "near zoom did not open every label");

  const near = recorder();
  drawScene(near.target as never, ladderScene(1.6).scene as never);
  assert.equal(labelCount(near.calls), HUB_FIELD.nodes.length);
});

test("tier 1 keeps its own label rule: everything labels at the old floor", () => {
  const { target, calls } = recorder();
  const { scene } = sceneOf({ view: { scale: 1.0, tx: 0, ty: 0 } });
  drawScene(target as never, scene as never);
  assert.ok(labelCount(calls) >= SMALL_FIELD.nodes.length, "the tier-1 floor moved");
});

test("the landed frame keeps the nodes the owner is holding and settles the rest", () => {
  const standing = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  pinNode(standing[2]!, 40, 50);

  const landed = seedPositions(SMALL_FIELD.nodes, [], 800, 600);
  for (const node of landed) {
    node.x += 100;
    node.y += 100;
  }
  preserveHeldNodes(landed, standing);

  const merged = landed[2]!;
  assert.equal(merged.x, 40, "the swap teleported a node the owner was holding");
  assert.equal(merged.y, 50);
  assert.equal(merged.fx, 40, "the held node lost its hold across the swap");
  assert.equal(landed[0]!.x, standing[0]!.x + 100, "a node nobody held lost its settled coordinate");
});

test("releasing a pin in the settled tier-2 field clears the hold without moving the node", async () => {
  const field = syntheticField(1200, [{ kind: "entry", id: SYNTHETIC_PIN_ID, x: 77, y: 88 }]);
  const landed = await precomputeLayout(field, { width: 1200, height: 900 });
  const pinned = landed.find((n) => n.id === SYNTHETIC_PIN_ID)!;
  const spot = { x: pinned.x, y: pinned.y };

  releasePinnedNodes(landed);

  assert.equal(pinned.fx, null);
  assert.equal(pinned.x, spot.x, "release teleported the node in the static field");
  assert.equal(pinned.y, spot.y);
});

test("a tier-2 field renders the honest notice and the release sentence, but no computing line", () => {
  const big = syntheticField(1201);
  const html = renderToStaticMarkup(
    React.createElement(GraphCanvas, {
      field: big,
      selectedKey: null,
      reducedMotion: false,
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /1201 nodes are above the 1000-node live-simulation ceiling/);
  assert.match(html, /releasing pinned nodes here leaves them where they stand/);
  assert.match(html, /runs on the precomputed tier/);
  assert.ok(!/Laying out /.test(html), "a static render claimed a settle it never kicked");

  const still = renderToStaticMarkup(
    React.createElement(GraphCanvas, {
      field: big,
      selectedKey: null,
      reducedMotion: true,
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.ok(!/Reduced motion is on/.test(still), "tier 2 has no motion for reduced motion to turn off");
});

test("tier-2 copy promises only what the slice transport does", () => {
  const computing = computingLayoutNotice(1500);
  assert.match(computing, /1500 nodes/);
  assert.match(computing, /slices/);
  const settled = precomputedNotice(1500, SIM_CEILING);
  const failed = settleFailedNotice(1500);
  for (const text of [computing, settled, failed]) {
    assert.ok(!/background|\bworker\b/i.test(text), `copy promised a transport it does not run: ${text}`);
  }
  assert.match(settled, /live-simulation ceiling/);
  assert.match(failed, /1500 nodes/);
  assert.match(failed, /seed sketch/, "failure copy must name the frame the owner is actually looking at");
  assert.ok(
    !/replaces it/.test(failed),
    "failure copy promised the settled swap that never happened",
  );
});

test("the precomputed branch kicks one settle and runs no frame clock of its own", () => {
  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.match(canvas, /setPrecomputing\(true\)/);
  assert.match(canvas, /const SETTLE_KICK_DELAY_MS = 200/, "the resize-noise beat moved or vanished");
  assert.match(canvas, /void precomputeLayout\(field, forces, \(\) => !live\)/, "the settle must be abandonable mid-flight");
  assert.match(canvas, /clearTimeout\(kick\)/, "a superseded kick must never start its settle");
  assert.match(canvas, /preserveHeldNodes\(landed, nodesRef\.current\)/);
  assert.match(canvas, /nodesRef\.current = landed/);
  assert.match(canvas, /precomputing\s*\?\s*computingLayoutNotice/, "the pending state lost its notice slot");
  assert.match(canvas, /settleFailed\s*\?\s*settleFailedNotice/, "the failed settle lost its honest notice");
  assert.match(canvas, /setSettleFailed\(true\)/, "failure keeps the seed but must not flip to the settled wording");
  assert.equal(
    (canvas.match(/requestAnimationFrame/g) ?? []).length,
    2,
    "the tier-1 loop's two rAF calls grew company — the precomputed branch runs no frame clock",
  );
});

test("an abandoned settle stops before it spends a tick: the frame comes back the seed", async () => {
  const field = syntheticField(1500, [{ kind: "entry", id: SYNTHETIC_PIN_ID, x: 77, y: 88 }]);
  const aborted = await precomputeLayout(field, { width: 1200, height: 900 }, () => true);
  const seed = seedPositions(field.nodes, field.positions, 1200, 900);
  const frame = (nodes: LayoutNode[]) => nodes.map((n) => [Math.round(n.x), Math.round(n.y)]);
  assert.deepEqual(frame(aborted), frame(seed), "an abandoned settle kept slicing");
});

test("a settle abandoned mid-flight keeps partial progress but never reaches the full frame", async () => {
  const field = syntheticField(1500);
  const forces = { width: 1200, height: 900 };
  const full = await precomputeLayout(field, forces);
  let checks = 0;
  const partial = await precomputeLayout(field, forces, () => {
    checks += 1;
    return checks > 1;
  });
  const frame = (nodes: LayoutNode[]) => nodes.map((n) => [Math.round(n.x), Math.round(n.y)]);
  const seed = seedPositions(field.nodes, [], 1200, 900);
  assert.notDeepEqual(frame(partial), frame(seed), "the token tripped before any slice ran");
  assert.notDeepEqual(frame(partial), frame(full), "the abandoned settle sliced to the end anyway");
});

test("the aria label names the tier the canvas actually runs", () => {
  assert.equal(tierName(layoutModeFor(5)), "live simulation");
  assert.equal(tierName(layoutModeFor(1201)), "precomputed");
  assert.equal(tierName(layoutModeFor(5001)), "clustered", "tier 3 paints clusters, so the aria must say clustered");

  assert.ok(!/tier/.test(graphAriaLabel(SMALL_FIELD.counts)), "the bare label grew a tier sentence");

  const html = renderToStaticMarkup(
    React.createElement(GraphCanvas, {
      field: SMALL_FIELD,
      selectedKey: null,
      reducedMotion: true,
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /runs on the live simulation tier/);
});

// ── T8: pins are rows on the Host, never bytes in the browser ─────────────────────────

// The files the pin door touched beyond the slice ledger: the wire module, the shared types and
// the hook that owns the mutation. Swept together with the slice below; the page joins by name
// at the end of the sweep.
const T8_PIN_FILES = [
  "api.ts",
  "queries.ts",
  "types.ts",
  "lib/use-graph.ts",
] as const;

const pinFile = (path: string) =>
  readFileSync(new URL(`../src/features/journal/${path}`, import.meta.url), "utf8");

const pageFile = () =>
  readFileSync(
    new URL("../src/pages/journal-graph-page/ui/journal-graph-page.tsx", import.meta.url),
    "utf8",
  );

test("no graph file and no pin-door file touches browser storage", () => {
  const offenders: string[] = [];
  for (const file of [...GRAPH_SLICE, ...T8_PIN_FILES]) {
    const source = sliceFile(file);
    if (/localStorage|sessionStorage|indexedDB/.test(source)) {
      offenders.push(file);
    }
  }
  if (/localStorage|sessionStorage|indexedDB/.test(pageFile())) {
    offenders.push("pages/journal-graph-page/ui/journal-graph-page.tsx");
  }
  assert.deepEqual(
    offenders,
    [],
    "a position stored in the browser is a layout only that browser sees — R5 puts the rows on the Host",
  );
});

test("the pin door is one PUT on the human prefix, answered by 204 and a refetch", () => {
  const api = pinFile("api.ts");
  assert.match(api, /apiPut<void>\("\/graph\/positions", \{ positions \}\)/);
  assert.ok(!/localStorage|sessionStorage/.test(api));

  const hooks = sliceFile("lib/use-graph.ts");
  assert.match(hooks, /invalidateQueries\(\{ queryKey: graphKeys\.all \}\)/);
  assert.match(
    hooks,
    /onError: \(\) => queryClient\.invalidateQueries\(\{ queryKey: graphKeys\.all \}\)/,
    "a failed PUT leaves the canvas promising a pin the Host never stored",
  );
  assert.ok(!/setQueryData/.test(hooks), "the read projection is the only pin truth the client keeps");
});

test("the canvas stores the pin when the drag settles, not while it moves", () => {
  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.match(canvas, /onPin\?\.\(\{ kind: node\.kind, id: node\.id, x: node\.x, y: node\.y \}\)/);
  const up = /const onPointerUp = [\s\S]*?onSelect\(pickAt\(x, y\)\);/;
  assert.match(canvas, up, "the settle handler lost its shape");
  const movePin = /const onPointerMove = [\s\S]*?pinNode\(node, toWorldX\(x, currentView\(\)\), toWorldY\(y, currentView\(\)\)\);[\s\S]*?const onPointerUp/;
  assert.match(canvas, movePin, "the drag loop must not be the place a row is written from");
  assert.match(canvas, /field\.positions\.length > 0[\s\S]{0,80}onUnpinAll\?\.\(\)/);
});

test("an unpin leaves the node where its simulation stood instead of reseeding it", () => {
  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.match(canvas, /before\.fx == null/, "only null or undefined fx preserves the standing spot");
  assert.ok(!/before\.fx === undefined/.test(canvas), "a released pin (fx: null) would teleport to the seed");
});

test("the Host route table still names exactly one positions verb, on the human group", () => {
  const api = readFileSync(
    new URL("../../src/Overmind.Host/Api/GraphApi.cs", import.meta.url),
    "utf8",
  );
  assert.match(api, /group\.MapPut\("\/graph\/positions", SavePositions\)/);
  const mapCalls = [...api.matchAll(/Map(?:Get|Put|Post|Delete|Patch)\("([^"]*positions[^"]*)"/g)]
    .map((match) => match[1]);
  assert.deepEqual(
    mapCalls,
    ["/graph/positions"],
    "a second positions verb is a boundary move — the route table owns the proof, this names the source",
  );
});

// ── Tier 3: the field becomes project clusters ────────────────────────────────────────

const fixturePid = (index: number) => `cluster-project-${index}`;
const fixtureEid = (project: number, index: number) => `cluster-note-${project}-${index}`;

// A synthetic tier-3 field: each project dot anchors its own entries, neighbouring projects
// share one cross link so anchor aggregation has something to fold, and the rest of the corpus
// is notes with no project at all.
const clusteredFixture = (projects: number, perProject: number, loose = 0): GraphField => {
  const nodes: GraphNode[] = [];
  const edges: GraphEdge[] = [];
  for (let p = 0; p < projects; p += 1) {
    nodes.push(node("project", fixturePid(p), `Cluster ${p}`, { projectId: fixturePid(p), entryType: null }));
    for (let i = 0; i < perProject; i += 1) {
      nodes.push(node("entry", fixtureEid(p, i), `Note ${p}.${i}`, { projectId: fixturePid(p) }));
      edges.push(edge("entry", fixtureEid(p, i), "project", fixturePid(p)));
    }
  }
  for (let u = 0; u < loose; u += 1) {
    nodes.push(node("entry", `loose-${u}`, `Loose ${u}`, { projectId: "" }));
  }
  for (let p = 0; p + 1 < projects; p += 1) {
    edges.push(edge("entry", fixtureEid(p, 0), "entry", fixtureEid(p + 1, 0)));
  }
  return fieldOf(nodes, edges);
};

const frameOf = (nodes: LayoutNode[]) => nodes.map((n) => [Math.round(n.x), Math.round(n.y)]);

test("clusters group by projectId and the project dot anchors its own group", () => {
  const base = clusteredFixture(2, 2, 2);
  const dangling = node("entry", "dangling-1", "Dangling note", { projectId: "absent-project" });
  const zeroed = node("entry", "zeroed-1", "Zero-guid note", {
    projectId: "00000000-0000-0000-0000-000000000000",
  });
  const field = fieldOf([...base.nodes, dangling, zeroed], base.edges);
  const model = buildClusters(field);

  const named = model.groups.filter((group) => group.key !== UNSORTED_CLUSTER_ID);
  assert.equal(named.length, 2, "one group per project dot, not one per node");
  assert.equal(named[0]!.anchor.id, fixturePid(0));
  assert.equal(named[0]!.members[0]!.id, fixturePid(0), "the project node anchors its own group");
  assert.equal(named[0]!.members.length, 3, "anchor plus its two entries");
  assert.equal(named[0]!.hiddenCount, 2, "the hidden count is what the collapsed dot stands in for");

  const unsorted = model.groups[model.groups.length - 1]!;
  assert.equal(unsorted.key, UNSORTED_CLUSTER_ID, "unanchorable nodes share one explicit cluster");
  assert.deepEqual(
    unsorted.members.map((member) => member.id),
    ["loose-0", "loose-1", "dangling-1", "zeroed-1"],
    "empty, zero-guid and dangling projectIds all land in the unsorted bucket",
  );
  assert.equal(unsorted.anchor.id, UNSORTED_CLUSTER_ID);
  assert.equal(unsorted.hiddenCount, unsorted.members.length);

  const accounted = model.groups.reduce((sum, group) => sum + group.members.length, 0);
  assert.equal(accounted, field.nodes.length, "the grouping is a partition, not a selection");
  assert.ok(model.groups.slice(0, -1).every((group) => group.key !== UNSORTED_CLUSTER_ID), "unsorted stays last");
});

test("anchor links fold member edges onto unordered anchor pairs and count them", () => {
  const base = clusteredFixture(3, 2, 0);
  const back = edge("entry", fixtureEid(1, 0), "entry", fixtureEid(0, 1));
  const field = fieldOf([...base.nodes], [...base.edges, back]);
  const rows = anchorLinks(field, buildClusters(field));

  const pair = (from: string, to: string) => rows.find(
    (row) => (row.from.id === from && row.to.id === to) || (row.from.id === to && row.to.id === from),
  );
  assert.equal(pair(fixturePid(0), fixturePid(1))?.count, 2, "two member edges, one of them reversed — one row");
  assert.equal(pair(fixturePid(1), fixturePid(2))?.count, 1);
  assert.equal(pair(fixturePid(0), fixturePid(2)), undefined, "unlinked anchors stay unlinked");
  assert.ok(rows.every((row) => row.from.kind === "project" && row.to.kind === "project"));

  const edges = anchorLinkEdges(rows);
  assert.ok(edges.every((row) => row.fromKind === "project" && row.toKind === "project"));
  assert.equal(edges.length, rows.length, "the draw-ready rows are the aggregation, not a second opinion");
});

test("the same field builds the same clusters and the same anchor frame twice", async () => {
  const field = clusteredFixture(6, 20, 4);
  const forces = { width: 1200, height: 900 };

  const firstModel = buildClusters(field);
  const secondModel = buildClusters(field);
  assert.deepEqual(firstModel.groups, secondModel.groups);
  const firstAnchors = anchorLinks(field, firstModel);
  assert.deepEqual(firstAnchors, anchorLinks(field, secondModel));

  const first = await precomputeLayout(anchorSetField(field, firstModel), forces);
  const second = await precomputeLayout(anchorSetField(field, secondModel), forces);
  assert.deepEqual(frameOf(first), frameOf(second), "the anchor layer settled into a different map");
  assert.equal(first.length, firstModel.groups.length, "the anchor frame is not the full field");
  assert.ok(first.every((n) => Number.isFinite(n.x) && Number.isFinite(n.y)));
});

test("clusterLabel keeps the count through truncation and the radius reflects it", () => {
  const long = "A project whose name runs long enough to threaten the label budget".repeat(2);
  const label = clusterLabel(long, 1247);
  assert.ok(label.length <= MAX_LABEL_CHARS, "the label outgrew the painter's truncation budget");
  assert.match(label, /\(1247\)$/, "the count did not survive truncation");
  assert.equal(truncateLabel(label), label, "the painter would cut the count off the frame");
  assert.equal(clusterLabel("Small", 3), "Small (3)");

  assert.equal(anchorRadius(0), NODE_RADIUS.project, "an anchor hiding nothing is a plain project dot");
  assert.ok(anchorRadius(1) > anchorRadius(0));
  assert.ok(anchorRadius(40) > anchorRadius(10));
  assert.equal(anchorRadius(10_000), 24, "the biggest project is still a dot among dots");
});

test("a 5001-node field paints only collapsed anchors, each honestly labeled Name (N)", () => {
  const field = clusteredFixture(50, 99, 1);
  assert.equal(layoutModeFor(field.nodes.length), "clustered", "the fixture left tier 3");
  const model = buildClusters(field);
  const composed = composeClusterView(field, model, new Set(), new Map(), new Map(), 1200, 900);
  const links = seedLayoutLinks(projectedLinks(field, model, new Set()), composed.nodes);

  const { target, calls } = recorder();
  drawScene(target as never, {
    nodes: composed.nodes,
    links,
    view: { scale: 1, tx: 0, ty: 0 },
    colors: paletteColors,
    states: graphNodeStates(field.nodes, field.edges),
    hoverKey: null,
    selectedKey: null,
    neighbours: null,
    width: 1200,
    height: 900,
    font: "12px ui-sans-serif, system-ui, sans-serif",
    tier: "clustered",
    anchors: composed.hidden,
  } as never);

  const paintedDots = calls.filter((call) => call.op === "fill" || call.op === "stroke");
  assert.ok(paintedDots.length > 0, "the clustered frame drew nothing");
  assert.equal(composed.nodes.length, model.groups.length, "the composition is not the full field");
  const labels = calls.filter((call) => call.op === "fillText").map((call) => call.text ?? "");
  assert.equal(labels.length, model.groups.length, "a collapsed anchor painted without its name");
  const counts = labels.map((label) => {
    const match = / \((\d+)\)$/.exec(label);
    assert.ok(match !== null, `an anchor label without an honest count: ${label}`);
    return Number(match![1]);
  });
  const realAnchors = model.groups.length - 1;
  assert.equal(
    counts.reduce((sum, count) => sum + count, 0),
    field.nodes.length - realAnchors,
    "the printed counts do not account for every node the anchors stand in for",
  );
  assert.equal(composed.hidden.size, model.groups.length, "every closed cluster claims its count");
});

test("expanding one cluster reveals exactly its members and settles them deterministically", async () => {
  const field = clusteredFixture(4, 30, 3);
  const model = buildClusters(field);
  const group = model.groups[0]!;
  const open = new Set([group.key]);
  const forces = { width: 1200, height: 900 };
  const origin = { x: 400, y: 300 };

  const view = visibleNodes(model, open);
  const visibleKeys = new Set(view.nodes.map((n) => nodeKey(n.kind, n.id)));
  const expected = model.groups.reduce(
    (sum, candidate) => sum + (open.has(candidate.key) ? candidate.members.length : 1),
    0,
  );
  assert.equal(view.nodes.length, expected);
  for (const member of group.members) {
    assert.ok(visibleKeys.has(nodeKey(member.kind, member.id)), "an expanded member stayed hidden");
  }
  const closed = model.groups[1]!;
  assert.ok(!visibleKeys.has(nodeKey(closed.members[1]!.kind, closed.members[1]!.id)), "a closed cluster leaked a member");
  assert.equal(view.hidden.get(group.anchorKey), undefined, "the open cluster still claims to hide members");
  assert.equal(view.hidden.get(closed.anchorKey), closed.hiddenCount);
  const openAnchor = view.nodes.find((n) => n.id === group.anchor.id)!;
  assert.equal(openAnchor.label, group.anchor.label, "the open anchor kept its count label");

  const bloomForces = { ...forces, centreX: origin.x, centreY: origin.y };
  const first = await precomputeLayout(clusterSubField(field, group), bloomForces, () => false, origin);
  const second = await precomputeLayout(clusterSubField(field, group), bloomForces, () => false, origin);
  assert.deepEqual(frameOf(first), frameOf(second), "the same bloom settled into a different map");
  const anchor = first.find((n) => n.id === group.anchor.id)!;
  assert.ok(Math.abs(anchor.x - origin.x) < 60 && Math.abs(anchor.y - origin.y) < 60, "the bloom left its anchor behind");

  const collapsed = visibleNodes(model, new Set());
  assert.equal(collapsed.nodes.length, model.groups.length, "collapse did not return to the anchor-only field");
  assert.equal(collapsed.hidden.get(group.anchorKey), group.hiddenCount, "collapse lost the count label");
});

test("an expansion abandoned at the slice boundary leaves the seeded bloom, not a half-settled frame", async () => {
  const field = clusteredFixture(1, 900, 0);
  const group = buildClusters(field).groups[0]!;
  const sub = clusterSubField(field, group);
  const forces = { width: 1200, height: 900 };
  const origin = { x: 600, y: 450 };

  const aborted = await precomputeLayout(sub, forces, () => true, origin);
  const seed = seedPositions(sub.nodes, sub.positions, forces.width, forces.height, origin);
  assert.deepEqual(frameOf(aborted), frameOf(seed), "an abandoned bloom kept slicing");
});

test("the composition is a stable map for the same field, the same open set and the same holds", () => {
  const field = clusteredFixture(3, 6, 2);
  const model = buildClusters(field);
  const open = new Set([model.groups[0]!.key, model.groups[2]!.key]);
  const anchors = new Map([[model.groups[0]!.anchorKey, { x: 100, y: 150 }]]);
  const standing = new Map([
    [nodeKey("entry", fixtureEid(0, 1)), { x: 7, y: 9, fx: 7, fy: 9 } as unknown as LayoutNode],
  ]);

  const first = composeClusterView(field, model, open, anchors, standing, 800, 600);
  const second = composeClusterView(field, model, open, anchors, standing, 800, 600);
  assert.deepEqual(frameOf(first.nodes), frameOf(second.nodes));

  const held = first.nodes.find((n) => n.id === fixtureEid(0, 1))!;
  assert.equal(held.x, 7, "a held dot was moved by the recomposition");
  const anchored = first.nodes.find((n) => n.id === model.groups[0]!.anchor.id)!;
  assert.equal(anchored.x, 100, "the settled anchor frame lost to the seed spiral");
  const fresh = first.nodes.find((n) => n.id === fixtureEid(2, 0))!;
  assert.ok(Number.isFinite(fresh.x) && Number.isFinite(fresh.y));
});

test("hover dims the rest of an open cluster to 30 percent and keeps the neighbourhood lit", () => {
  const field = clusteredFixture(3, 4, 0);
  const model = buildClusters(field);
  const open = new Set([model.groups[0]!.key]);
  const composed = composeClusterView(field, model, open, new Map(), new Map(), 800, 600);
  const links = seedLayoutLinks(projectedLinks(field, model, open), composed.nodes);
  const hoverKey = nodeKey("entry", fixtureEid(0, 1));
  const neighbours = buildNeighbourhoods(links).get(hoverKey);

  const { target, calls } = recorder();
  drawScene(target as never, {
    nodes: composed.nodes,
    links,
    view: { scale: 1, tx: 0, ty: 0 },
    colors: paletteColors,
    states: graphNodeStates(field.nodes, field.edges),
    hoverKey,
    neighbours: neighbours?.keys ?? new Set<string>(),
    width: 800,
    height: 600,
    font: "12px ui-sans-serif, system-ui, sans-serif",
    tier: "clustered",
    anchors: composed.hidden,
  } as never);

  const touched = calls.filter((call) => call.op === "fill" || call.op === "stroke");
  assert.ok(touched.some((call) => call.globalAlpha === 0.3), "nothing dimmed inside the open cluster");
  assert.ok(touched.some((call) => call.globalAlpha === 1), "the hovered neighbourhood dimmed too");
  assert.ok(
    neighbours !== undefined && neighbours.keys.has(nodeKey("project", fixturePid(0))),
    "the hovered member lost its own anchor edge",
  );
});

test("the clustered tier renders honestly at 5001 nodes and never starts a frame clock", () => {
  const field = clusteredFixture(50, 99, 1);
  const model = buildClusters(field);
  const html = renderToStaticMarkup(
    React.createElement(GraphCanvas, {
      field,
      selectedKey: null,
      reducedMotion: false,
      onSelect: noop,
      onOpenNote: noop,
    }),
  );
  assert.match(html, /5001 nodes are above the 5000-node precompute ceiling/);
  assert.match(html, /grouped into clusters instead of drawn one dot per node/);
  assert.match(html, /runs on the clustered tier/);
  assert.match(html, new RegExp(`grouped into ${model.groups.length} clusters`));
  assert.ok(
    !/so nothing is animating/.test(html),
    "the tier-2 bridge wording survived into tier 3's own notice slot",
  );
  assert.match(html, /click or press Enter to open the group it hides and again to close it/);
  assert.match(html, /a reload starts with every cluster closed/);

  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.equal(
    (canvas.match(/requestAnimationFrame/g) ?? []).length,
    2,
    "tier 3 grew a frame clock — two rAF calls belong to the tier-1 loop and nothing else",
  );
  assert.match(canvas, /anchorSetField\(field, clusters\)/, "the anchor settle left the sliced transport");
  assert.match(canvas, /clusterSubField\(field, group\)/, "the expansion settle left the sliced transport");
  assert.match(canvas, /expansionGenRef\.current !== gen/, "the expansion settle must be abandonable mid-flight");
  assert.match(canvas, /\(\) => !live/, "the anchor settle must be abandonable mid-flight");
  assert.match(canvas, /const SETTLE_KICK_DELAY_MS = 200/, "the resize-noise beat moved or vanished");
});

test("keyboard and pointer cover the visible set, and the anchor is a door on both paths", () => {
  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.match(
    canvas,
    /const visible = tier === "clustered" \? nodesRef\.current : field\.nodes;/,
    "the arrow walk left the full payload list",
  );
  assert.match(canvas, /event\.key === "Enter" && tier === "clustered"/, "Enter lost its clustered door");
  assert.match(canvas, /const door = tier === "clustered" \? clusterDoor\(pickAt\(x, y\)\) : null;/);
  assert.match(canvas, /onSelect\(pickAt\(x, y\)\);/, "the click that selects a dot lost its shape");
  assert.match(canvas, /node\.id !== UNSORTED_CLUSTER_ID/, "a phantom anchor can reach the pin door");
});

// ── Review-271-03 fix round: settle bookkeeping and a hover-stable kick ───────────────

test("a bailed-out settle still closes its bookkeeping slot, and the kick ignores hover", () => {
  const canvas = sliceFile("components/graph-canvas.tsx");

  // MAJOR-1a: the decrement leads every settle exit — a callback whose first statement is a
  // guard would hold the notice slot open forever the first time a settle was abandoned.
  const landedFirst = canvas.match(/\(landed\) => \{\n\s*endSettle\(\);/g) ?? [];
  assert.equal(landedFirst.length, 2, "a settle callback kept its decrement behind its guard");
  const failedFirst = canvas.match(/\(\) => \{\n\s*endSettle\(\);/g) ?? [];
  assert.equal(failedFirst.length, 2, "a failure path kept its decrement behind its guard");

  // MAJOR-1b: the anchor kick's effect deps carry no draw identity. Hover restarts any effect
  // that lists draw, and a restarted kick whose set already "changed" would abandon its
  // in-flight settle and never run again — the field stranded on the seed spiral.
  assert.match(
    canvas,
    /\[beginSettle, clusters, endSettle, field, recomposeClusterField, size\]/,
    "the anchor kick's deps grew a hover identity",
  );
  assert.match(canvas, /drawRef\.current\(\);\n\s*\}\)\n\s*\.catch\(\(\) => \{\n\s*endSettle\(\);/, "the anchor settle lost the ref-drawn landing");
  assert.match(
    canvas,
    /settledAnchorSetRef\.current = anchorSet;/,
    "the landed-set guard vanished — an aborted settle would never re-kick",
  );

  // SUGGESTION-1: the bloom launches from a handler and has no effect cleanup to die with, so
  // its token and its exits carry the mount flag the kick's `live` provides.
  assert.match(canvas, /const mountedRef = useRef\(true\)/);
  assert.match(canvas, /!mountedRef\.current \|\| expansionGenRef\.current !== gen/, "the bloom token ignores unmount");
});

test("tier-3 settle notices name the subset they lay out, not the whole field", () => {
  const anchors = computingAnchorsNotice(51);
  assert.match(anchors, /51 cluster anchors/);
  assert.match(anchors, /slices/);
  const bloom = computingBloomNotice(31);
  assert.match(bloom, /Opening a cluster of 31 nodes/);
  for (const text of [anchors, bloom]) {
    assert.ok(!/background|\bworker\b/i.test(text), `tier-3 copy promised a transport it does not run: ${text}`);
  }

  const canvas = sliceFile("components/graph-canvas.tsx");
  assert.match(canvas, /layoutWhat === "anchors"/, "the anchor settle lost its own notice wording");
  assert.match(canvas, /computingAnchorsNotice\(layoutCount\)/);
  assert.match(canvas, /computingBloomNotice\(layoutCount\)/);
  assert.match(canvas, /beginSettle\(clusters\.groups\.length, "anchors"\)/);
  assert.match(canvas, /beginSettle\(group\.members\.length, "bloom"\)/);
});
