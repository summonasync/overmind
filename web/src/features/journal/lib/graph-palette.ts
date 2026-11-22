import type { GraphNode } from "../types.ts";

// The canvas paints with the app's tokens or it does not paint. R3 says no new colour tokens,
// so every value below is a name the shipped stylesheet already defines; the pin in
// `web/tests/journal-graph.test.mts` reads this list against `globals.css` and goes red on a
// rename, which is the "breaks a test, not silently the palette" half of that acceptance row.
//
// No literal colour lives in this module or anywhere in the slice — the hex sweep in
// `journal-note-view.test.mts` covers the graph files for exactly that reason.
export const GRAPH_TOKENS = {
  note: "--color-type-note",
  decision: "--color-type-decision",
  idea: "--color-type-idea",
  project: "--color-primary",
  feature: "--color-ceremony-goal",
  // The amended R3 pair reuses two tokens the theme already ships, so the resting edge reads
  // the same name as the dashed ring and the incident edge the same name as a label. Two
  // semantic slots, one token each: the ladder stays a contrast step (5.86:1 then 15.57:1)
  // without R3's "no new colour tokens" being quietly broken.
  edge: "--color-muted",
  edgeStrong: "--color-foreground",
  selected: "--color-primary",
  canvas: "--color-background",
  label: "--color-foreground",
  muted: "--color-muted",
} as const;

export type GraphSemanticColor = keyof typeof GRAPH_TOKENS;

const SEMANTIC_NAMES = Object.keys(GRAPH_TOKENS) as GraphSemanticColor[];

export interface ResolvedPalette {
  colors: Record<GraphSemanticColor, string>;
  unresolved: GraphSemanticColor[];
}

const VAR_REFERENCE = /^var\(\s*(--[\w-]+)\s*\)$/;

// `--color-ceremony-goal` is declared as `var(--color-success)`, and whether a browser hands
// back the reference or the substituted value is not something this app gets to choose, so both
// spellings are followed to a paintable value. The step cap is there because a cycle in the
// chain would otherwise hang the tab on the first frame.
function follow(value: string, read: (name: string) => string, maxSteps = 8): string {
  let current = value.trim();
  for (let step = 0; step < maxSteps; step += 1) {
    const match = VAR_REFERENCE.exec(current);
    if (match === null) {
      return current;
    }
    current = read(match[1]).trim();
    if (current.length === 0) {
      return "";
    }
  }
  return "";
}

export function resolvePalette(read: (name: string) => string): ResolvedPalette {
  const colors = {} as Record<GraphSemanticColor, string>;
  const unresolved: GraphSemanticColor[] = [];

  for (const name of SEMANTIC_NAMES) {
    const value = follow(read(GRAPH_TOKENS[name]), read);
    if (value.length === 0) {
      unresolved.push(name);
      colors[name] = "";
    } else {
      colors[name] = value;
    }
  }

  return { colors, unresolved };
}

// The only browser-facing line in this module, and it is a reader injected into the pure
// resolver above rather than a document reference inside it — that is what lets the token map be
// measured by `node --test` without a canvas.
export function paletteForDocument(): ResolvedPalette {
  if (typeof document === "undefined") {
    return { colors: {} as Record<GraphSemanticColor, string>, unresolved: SEMANTIC_NAMES };
  }
  const styles = getComputedStyle(document.documentElement);
  return resolvePalette((name) => styles.getPropertyValue(name));
}

export const NODE_RADIUS = {
  entry: 6,
  feature: 7,
  project: 9,
} as const;

export function nodeRadius(node: GraphNode): number {
  return NODE_RADIUS[node.kind];
}

// Tier 3 sizes a collapsed cluster by what it stands in for, on the same token-driven terms as
// the base radii: the growth follows the hidden count's square root — twice the nodes read
// visibly wider without letting a thousand-member project swallow the frame — and the cap keeps
// even the biggest project a dot among dots.
export function anchorRadius(hiddenCount: number): number {
  return Math.min(24, NODE_RADIUS.project + Math.sqrt(Math.max(hiddenCount, 0)) * 1.6);
}

// Which token paints which dot, in one place: the canvas, the legend swatches and the list
// markers all ask here, so the legend cannot describe a colour the field does not wear. The
// legend counts its own rows off this function rather than restating the predicate, which is
// what makes "5 notes / 2 decisions" a measurement of the payload instead of a second opinion.
export function nodeColorName(node: GraphNode): GraphSemanticColor {
  if (node.kind === "project") {
    return "project";
  }
  if (node.kind === "feature") {
    return "feature";
  }
  if (node.entryType === "decision") {
    return "decision";
  }
  return node.entryType === "idea" ? "idea" : "note";
}

export const LEGEND_NODE_ROWS: readonly { color: GraphSemanticColor; label: string }[] = [
  { color: "note", label: "Notes" },
  { color: "decision", label: "Decisions" },
  { color: "idea", label: "Ideas" },
  { color: "project", label: "Projects" },
  { color: "feature", label: "Features" },
];
