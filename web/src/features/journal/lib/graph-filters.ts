import type { GraphEntryType, GraphViewFilters } from "../types.ts";

// The graph's state is the URL, exactly as the inbox does it: a reload, a back gesture and a
// shared link must open the same field. This module owns the grammar and nothing else, so
// `node --test` can reach it without a canvas.
//
// The parameter names are the endpoint's own (`GraphApi.ReadGraph`'s bound parameters), and
// `lib/graph-query.ts` sends them from the same list — the drift pin in
// `web/tests/journal-graph.test.mts` reads both sides off disk.
export const GRAPH_QUERY_KEYS = [
  "type",
  "project",
  "focus",
  "depth",
  "orphansOnly",
  "includeDone",
  "search",
] as const;

export type GraphQueryKey = (typeof GRAPH_QUERY_KEYS)[number];

// R1's universe narrowed to what the endpoint will accept; anything else 400s
// (UnknownGraphTypeFilter) and the URL degrades to "no narrowing" instead of asking for it.
export const GRAPH_ENTRY_TYPES: readonly GraphEntryType[] = ["journal", "decision", "idea"];

const FOCUS_SHAPE = /^(entry|project|feature):[0-9a-fA-F][0-9a-fA-F-]{30,38}$/;
const DEPTH_SHAPE = /^[1-9]\d{0,2}$/;

export const EMPTY_GRAPH_FILTERS: GraphViewFilters = {
  types: [],
  project: "",
  focus: "",
  depth: "",
  orphansOnly: false,
  includeDone: false,
  search: "",
};

function readTypes(raw: string | null): GraphEntryType[] {
  const seen = new Set<GraphEntryType>();
  for (const value of (raw ?? "").split(",")) {
    const trimmed = value.trim().toLowerCase();
    const match = GRAPH_ENTRY_TYPES.find((type) => type === trimmed);
    if (match !== undefined) {
      seen.add(match);
    }
  }
  return [...seen];
}

function readFlag(raw: string | null): boolean {
  const value = raw?.trim().toLowerCase() ?? "";
  return value === "true" || value === "1";
}

// A hand-typed focus is kept only when it is a node handle the endpoint can answer. Depth is
// meaningless without a focus (`DepthWithoutFocus`), so an orphaned depth drops with it.
function readFocus(raw: string | null): string {
  const value = raw?.trim() ?? "";
  return FOCUS_SHAPE.test(value) ? value : "";
}

function readDepth(raw: string | null, focus: string): string {
  const value = raw?.trim() ?? "";
  if (focus.length === 0) {
    return "";
  }
  return DEPTH_SHAPE.test(value) ? value : "";
}

export function parseGraphFilters(source: URLSearchParams | null): GraphViewFilters {
  const focus = readFocus(source?.get("focus") ?? null);
  return {
    types: readTypes(source?.get("type") ?? null),
    project: source?.get("project")?.trim() ?? "",
    focus,
    depth: readDepth(source?.get("depth") ?? null, focus),
    orphansOnly: readFlag(source?.get("orphansOnly") ?? null),
    includeDone: readFlag(source?.get("includeDone") ?? null),
    search: source?.get("search")?.trim() ?? "",
  };
}

export function writeGraphFilters(
  target: URLSearchParams,
  filters: GraphViewFilters,
): URLSearchParams {
  if (filters.types.length > 0) {
    target.set("type", filters.types.join(","));
  } else {
    target.delete("type");
  }

  if (filters.project.length > 0) {
    target.set("project", filters.project);
  } else {
    target.delete("project");
  }

  if (filters.focus.length > 0) {
    target.set("focus", filters.focus);
    if (filters.depth.length > 0) {
      target.set("depth", filters.depth);
    } else {
      target.delete("depth");
    }
  } else {
    target.delete("focus");
    target.delete("depth");
  }

  setFlag(target, "orphansOnly", filters.orphansOnly);
  setFlag(target, "includeDone", filters.includeDone);

  if (filters.search.length > 0) {
    target.set("search", filters.search);
  } else {
    target.delete("search");
  }

  return target;
}

function setFlag(target: URLSearchParams, key: string, value: boolean): void {
  if (value) {
    target.set(key, "true");
  } else {
    target.delete(key);
  }
}

// The chips have to show that something is narrowing the field even when only a flag is set,
// so "active" means "differs from the empty filter" rather than "has a text value".
export function isGraphFilterActive(filters: GraphViewFilters): boolean {
  return (
    filters.types.length > 0 ||
    filters.project.length > 0 ||
    filters.focus.length > 0 ||
    filters.orphansOnly ||
    filters.includeDone ||
    filters.search.length > 0
  );
}

export function toggleGraphEntryType(
  types: readonly GraphEntryType[],
  type: GraphEntryType,
): GraphEntryType[] {
  const next = new Set<GraphEntryType>(types);
  if (next.has(type)) {
    next.delete(type);
  } else {
    next.add(type);
  }
  return GRAPH_ENTRY_TYPES.filter((candidate) => next.has(candidate));
}

// The endpoint reads `focus=entry:<id>`; a node knows its own kind and id, so the handle is
// built here rather than at every call site.
export function focusHandleOf(kind: string, id: string): string {
  return `${kind}:${id}`;
}

export function focusEntryId(handle: string): string {
  const at = handle.indexOf(":");
  return at < 0 ? "" : handle.slice(at + 1);
}

// Orphans-only and a focus window contradict each other (`OrphansWithFocus` 400s the pair), so
// turning one on takes the other off instead of shipping a request the server will refuse.
export function withOrphansOnly(filters: GraphViewFilters, orphansOnly: boolean): GraphViewFilters {
  if (!orphansOnly) {
    return { ...filters, orphansOnly };
  }
  return { ...filters, orphansOnly: true, focus: "", depth: "" };
}

export function withFocus(filters: GraphViewFilters, handle: string): GraphViewFilters {
  if (handle.length === 0) {
    return { ...filters, focus: "", depth: "", orphansOnly: false };
  }
  return { ...filters, focus: handle, depth: filters.depth, orphansOnly: false };
}

// Wording duty (spec 265 owns the matching, this card only owns its own strings): SQLite LIKE
// folds case for ASCII only, so nothing on this surface may promise that case is ignored. The
// control says what it does — matches the title as typed — and the empty-result line names the
// term as typed.
export function graphSearchLabel(): string {
  return "Title contains";
}

export function graphSearchEmptyMessage(term: string): string {
  const typed = term.trim();
  return typed.length === 0
    ? "No nodes match these filters."
    : `No node title contains "${typed}" as typed. Try the exact case you used, or clear the search.`;
}
