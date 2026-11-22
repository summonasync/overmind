import type { LayoutMode } from "./graph-layout.ts";
import type { GraphCounts, GraphNode, GraphNodeState, GraphViewFilters } from "../types.ts";

// Every string the graph surface shows, in one module: the a11y label, the legend, the ceiling
// notice and the control copy are pinned from here, and the wording duty below is checked
// against this file rather than hunted through the components.
//
// Wording duty. SQLite `LIKE` folds case for ASCII only — Cyrillic titles match exactly as
// typed, and this owner's corpus is Cyrillic. Nothing here may promise that case is ignored;
// the search control says what it does. The matching itself is feature 265's, not this card's.
export const GRAPH_SEARCH_HINT = "Matches the title text as typed.";

const ASCII_CASE_NOTE =
  "Titles are matched as typed: the store folds case for Latin letters only, so try the exact spelling you used.";

export function graphAriaLabel(counts: GraphCounts, tier?: string): string {
  const byKind =
    `${counts.entryNodes} entries, ${counts.projectNodes} projects, ${counts.featureNodes} features`;
  const links = counts.edges === 1 ? "1 link" : `${counts.edges} links`;
  return (
    `Graph map: ${counts.nodes} nodes (${byKind}) joined by ${links}. ` +
    `${counts.orphans} nodes carry no links at all, ${counts.isolatedInView} link only to something outside this view. ` +
    "The list below holds the same data and is the keyboard path." +
    (tier === undefined ? "" : ` The field runs on the ${tier} tier.`)
  );
}

// The tier name the canvas is allowed to say out loud. T3 owns clusters; until it ships, a field
// past the precompute ceiling is drawn as the static seed and named "static" — calling it
// "clustered" would promise grouping the canvas never paints.
export function tierName(mode: LayoutMode): string {
  if (mode === "simulate") {
    return "live simulation";
  }
  return mode === "precomputed" ? "precomputed" : "static";
}

// A NODE count, never a link count: the dropped-edge tally is not in the payload
// (`GraphContracts.cs`), so the legend says what the number can back.
export function isolatedInViewLabel(count: number): string {
  return count === 1
    ? "1 entry here links to something outside this view"
    : `${count} entries here link to something outside this view`;
}

export function orphanLabel(count: number): string {
  return count === 1 ? "1 node has no links at all" : `${count} nodes have no links at all`;
}

export function nodeCountLabel(counts: GraphCounts): string {
  return `${counts.nodes} nodes · ${counts.edges === 1 ? "1 link" : `${counts.edges} links`}`;
}

// R9: the ceiling notice names the real count instead of quietly going static. Under the ladder
// (271) the canvas renders this line for every over-ceiling count until each tier ships its own
// notice below, so the only layout claim it makes is one true across all three modes.
export function ceilingMessage(nodeCount: number, ceiling: number): string {
  const head = nodeCount === 1 ? "1 node is" : `${nodeCount} nodes are`;
  return (
    `${head} above the ${ceiling}-node live-simulation ceiling, so nothing is animating. ` +
    "The field is drawn from a computed layout and the list below stays the primary path."
  );
}

// The ladder's per-tier notices. Each names the rung it crossed and what the canvas does about
// it, and both keep the live-simulation-ceiling phrase — the copy pin sweeps the island for
// that string, and the honest ladder still leads with it.
//
// Tier 2 draws its seed sketch first and swaps in the settled map when the layout lands, so the
// notice says that order and not "before its first frame". It also promises nothing about
// background threads: the shipped transport is time-sliced work on the main thread (see the
// transport comment in graph-precompute.ts), and release in this tier leaves nodes where they
// stand because there is no simulation left to return them to.
export function precomputedNotice(nodeCount: number, ceiling: number): string {
  const head = nodeCount === 1 ? "1 node is" : `${nodeCount} nodes are`;
  return (
    `${head} above the ${ceiling}-node live-simulation ceiling, so the field draws its seed ` +
    "sketch first and settles once, off the render loop, into the map that replaces it. " +
    "Nothing is animating, and releasing pinned nodes here leaves them where they stand — " +
    "this tier has no simulation to return them to. The list below stays the primary path."
  );
}

// Shown only while the tier-2 settle is still slicing. The wording keeps to what the transport
// does: the work runs in bounded slices with input handled between them, which is not the same
// claim as an idle page, and the field below is the seed sketch until the swap.
export function computingLayoutNotice(nodeCount: number): string {
  const noun = nodeCount === 1 ? "node" : "nodes";
  return (
    `Laying out ${nodeCount} ${noun}: the settle runs in slices between frames, so input is ` +
    "handled between them. The field below is the seed sketch until the layout lands."
  );
}

// The settle threw and stayed abandoned, so the seed sketch is the field and no swap is coming —
// the settled-tier wording would promise "the map that replaces it", which is exactly the swap
// that never happened. The sketch is not a broken frame: every node is on it, nothing animates,
// and the same kick runs again on the next field beat or reload.
export function settleFailedNotice(nodeCount: number): string {
  const noun = nodeCount === 1 ? "node" : "nodes";
  return (
    `The layout for ${nodeCount} ${noun} did not finish, so the field is showing its seed ` +
    "sketch: every node is on it, nothing is animating, and the list below stays the primary path. " +
    "Refetching the graph or reloading retries the layout."
  );
}

export function clusteredNotice(nodeCount: number, ceiling: number): string {
  const head = nodeCount === 1 ? "1 node is" : `${nodeCount} nodes are`;
  return (
    `${head} above the ${ceiling}-node precompute ceiling — past the live-simulation ceiling as well — ` +
    "so the field is grouped into clusters instead of drawn one dot per node. The list below stays the primary path."
  );
}

export function reducedMotionMessage(nodeCount: number): string {
  const noun = nodeCount === 1 ? "node settles" : "nodes settle";
  return `Reduced motion is on: the ${nodeCount} ${noun} before the first frame, with the same links and the same list.`;
}

export function emptyFieldMessage(hasSearch: boolean, term: string): string {
  if (!hasSearch) {
    return "No nodes match these filters. Widen them, or capture a note first.";
  }
  const typed = term.trim();
  return `No node title contains "${typed}" as typed. ${ASCII_CASE_NOTE}`;
}

export function unsupportedRowsNotice(nodes: number, edges: number): string {
  if (nodes === 0 && edges === 0) {
    return "";
  }
  const parts: string[] = [];
  if (nodes > 0) {
    parts.push(`${nodes} node row${nodes === 1 ? "" : "s"} this build cannot draw`);
  }
  if (edges > 0) {
    parts.push(`${edges} link row${edges === 1 ? "" : "s"} whose endpoint is not in this view`);
  }
  return `The payload also carried ${parts.join(" and ")}. The endpoint vocabulary grew past this surface.`;
}

// The focus-refusal detail, framed as the kind of list it is.
//
// `GraphApi.ActiveCoFilters` enumerates every narrowing filter that was ACTIVE on the request, not
// the subset that removed the node: `?focus=entry:<decision>&type=journal` answers naming `'type'`
// and `'includeDone'`, although widening `includeDone` would not bring that node back
// (review-237-05 M1, measured on the built pipeline). The list is truthful as a superset — the real
// culprit is always in it — so the server sentence is rendered verbatim and this line says what it
// is. Nothing on the surface may promote one of its names to the cause.
export const FILTER_DETAIL_FRAMING =
  "These are the filters the field was narrowed by. Widening one of them may bring the node back; " +
  "the list names what was active, not which one dropped this node.";

// The framing is checkable against the controls under it, so it reads from the same filter state
// the chips do rather than from a paraphrase of the server sentence.
export function activeFilterNames(filters: GraphViewFilters): string[] {
  const names: string[] = [];

  if (filters.types.length > 0) {
    names.push("entry types");
  }
  if (filters.project.length > 0) {
    names.push("project");
  }
  if (filters.search.trim().length > 0) {
    names.push("title search");
  }
  if (filters.orphansOnly) {
    names.push("unlinked only");
  }
  // Not-inverted on purpose: Done features are hidden unless the owner widened them, so "hidden" is
  // the active narrowing at the default setting — which is exactly the case M1 was measured in.
  if (!filters.includeDone) {
    names.push("Done features hidden");
  }
  if (filters.focus.length > 0) {
    names.push("focus window");
  }

  return names;
}

export function nodeKindLabel(node: GraphNode): string {
  if (node.kind === "project") {
    return "Project";
  }
  if (node.kind === "feature") {
    return "Feature";
  }
  if (node.entryType === "decision") {
    return "Decision";
  }
  if (node.entryType === "idea") {
    return "Idea";
  }
  return "Journal note";
}

export function stateLabel(state: GraphNodeState): string {
  if (state === "orphan") {
    return "no links";
  }
  return state === "offView" ? "links leave this view" : "linked here";
}

export const GRAPH_CONTROL_LABELS = {
  zoomIn: "Zoom in",
  zoomOut: "Zoom out",
  fit: "Fit field",
  reset: "Release pinned nodes",
  centre: "Centre on this node",
  clearCentre: "Clear centre",
  orphansOnly: "Unlinked only",
  includeDone: "Include Done features",
  openNote: "Open note",
  list: "List of the same nodes",
} as const;

// R4's panel preview. Cut on code points and on a block boundary: a panel that shows half a
// fenced block is worse than one that shows the paragraph before it, and a UTF-16 unit cut can
// split a surrogate pair the viewer then renders as a replacement character.
export const PANEL_PREVIEW_CODEPOINTS = 400;

export function noteExcerpt(content: string, max = PANEL_PREVIEW_CODEPOINTS): string {
  const blocks = content.split(/\n\s*\n/);
  let text = "";
  for (const block of blocks) {
    const next = text.length === 0 ? block : `${text}\n\n${block}`;
    if (Array.from(next).length > max) {
      break;
    }
    text = next;
  }
  if (text.length === 0) {
    const chars = Array.from(blocks[0] ?? "");
    return `${chars.slice(0, max).join("")}${chars.length > max ? "…" : ""}`;
  }
  return text;
}

