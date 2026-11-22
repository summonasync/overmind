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
  emptyFieldMessage,
  graphAriaLabel,
  isolatedInViewLabel,
  nodeCountLabel,
  nodeKindLabel,
  noteExcerpt,
  orphanLabel,
  stateLabel,
  unsupportedRowsNotice,
} from "../src/features/journal/lib/graph-copy.ts";
import {
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
  GRAPH_TOKENS,
  LEGEND_NODE_ROWS,
  nodeColorName,
  resolvePalette,
} from "../src/features/journal/lib/graph-palette.ts";
import { drawScene, truncateLabel } from "../src/features/journal/lib/graph-paint.ts";
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

test("the ceiling is 1000 nodes and the mode flips above it", () => {
  assert.equal(SIM_CEILING, 1000);
  assert.equal(layoutModeFor(1000), "simulate");
  assert.equal(layoutModeFor(1001), "settled");
  assert.equal(isOverCeiling(1200), true);
  assert.match(ceilingMessage(1204, SIM_CEILING), /1204 nodes are above the 1000-node/);
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
    fillText: () => mark("fillText"),
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
  assert.equal(GRAPH_SLICE.length, 14);
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
