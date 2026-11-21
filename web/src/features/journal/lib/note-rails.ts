import type {
  JournalBacklink,
  JournalLinkKind,
  JournalOutgoingLink,
} from "../types.ts";

export function noteHref(id: string): string {
  return `/journal?note=${encodeURIComponent(id)}`;
}

export function projectHref(id: string): string {
  return `/dashboard?project=${encodeURIComponent(id)}`;
}

export function dayHref(day: string): string {
  return `/journal?day=${encodeURIComponent(day)}`;
}

// The graph route records itself in the note address it navigates to, and `gq` carries the
// filters the field was showing. Without the pair, "double-click a note, read it, tap back"
// lands the owner in the inbox with the map's filters gone — the round trip the graph's own
// acceptance row asks for. Kept to the two names this app owns: no arbitrary origin URL is
// accepted, so the param cannot be steered anywhere else.
export const GRAPH_ORIGIN = "journal-graph";

export function graphHrefFrom(graphSearch: string): string {
  const params = new URLSearchParams(graphSearch.startsWith("?") ? graphSearch.slice(1) : graphSearch);
  params.delete("note");
  const query = params.toString();
  return query.length > 0 ? `/journal/graph?${query}` : "/journal/graph";
}

export function noteHrefFromGraph(id: string, graphSearch: string): string {
  const carried = new URLSearchParams(graphSearch.startsWith("?") ? graphSearch.slice(1) : graphSearch);
  carried.delete("note");
  const target = new URLSearchParams({ note: id, from: GRAPH_ORIGIN });
  const query = carried.toString();
  if (query.length > 0) {
    target.set("gq", query);
  }
  return `/journal?${target.toString()}`;
}

// The back link goes home with the filters the owner arrived on, minus `note` — losing the
// day or tag chip they had set is the kind of thing that makes a stream feel like it is
// fighting back. A note opened from the graph goes back to the graph instead, with the field
// it was showing.
export function streamHrefFrom(search: string): string {
  const params = paramsOf(search);
  params.delete("note");
  if (params.get("from") === GRAPH_ORIGIN) {
    const carried = params.get("gq") ?? "";
    return graphHrefFrom(carried);
  }
  const query = params.toString();
  return query.length > 0 ? `/journal?${query}` : "/journal";
}

// The label has to answer the same question the href answers. A note opened off the canvas came
// from the map, and a back link that says "Journal" while it takes the owner to `/journal/graph`
// is the surface lying about where it is standing.
export function backLinkLabelFrom(search: string): string {
  return paramsOf(search).get("from") === GRAPH_ORIGIN ? "Back to graph map" : "Journal";
}

function paramsOf(search: string): URLSearchParams {
  return new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
}

export interface OutgoingGroups {
  notes: JournalOutgoingLink[];
  projects: JournalOutgoingLink[];
  features: JournalOutgoingLink[];
  tasks: JournalOutgoingLink[];
  // Kinds the wire grew past this union. The bucket is honest about being unrecognised instead
  // of guessing a heading — guessing is what made a task read as a feature (review-237-01 M2).
  other: JournalOutgoingLink[];
}

// A `Record` over the full kind union, so widening `JournalLinkKind` reddens `tsc` here rather
// than quietly routing the new member into the fallback bucket at runtime.
const OUTGOING_BUCKETS: Record<JournalLinkKind, keyof OutgoingGroups> = {
  entry: "notes",
  project: "projects",
  feature: "features",
  task: "tasks",
};

export function groupOutgoing(links: readonly JournalOutgoingLink[]): OutgoingGroups {
  const groups: OutgoingGroups = { notes: [], projects: [], features: [], tasks: [], other: [] };
  for (const link of links) {
    groups[OUTGOING_BUCKETS[link.kind] ?? "other"].push(link);
  }
  return groups;
}

// A feature row carries its guid and its title — `EntryLinkReader.LoadTargetLabelsAsync`
// labels features with `Title`, so neither the slug nor the owning project survives into the
// DTO, and `/dashboard?project=…&feature=<slug>` cannot be built from what the rail has. The
// row stays honest and unclickable rather than guessing a link; the missing fields are filed
// against the backend, not bolted on here.
//
// A task row is the same shape of gap and the reason the fallback exists at all: the promotion
// edge names the task by guid and title, while this app addresses a task as
// `<feature slug>/T<order>`, and the DTO carries neither half of that address.
export function outgoingHref(link: JournalOutgoingLink): string | null {
  switch (link.kind) {
    case "entry":
      return noteHref(link.id);
    case "project":
      return projectHref(link.id);
    case "feature":
    case "task":
      return null;
    default:
      return null;
  }
}

// The dead rows say why they are dead instead of just refusing the click; `null` means the row
// has a real link and the rail renders it as one.
export function outgoingDeadEndReason(link: JournalOutgoingLink): string | null {
  if (outgoingHref(link) !== null) {
    return null;
  }
  if (link.kind === "task") {
    return "This note was promoted into a board task. The detail payload labels it by title and guid only — a task is addressed by its feature slug and order, so no URL can be built from what the rail was given.";
  }
  if (link.kind === "feature") {
    return "The detail payload labels this edge by title and guid only — no slug to build a link from.";
  }
  return `The Host reported a link kind this build does not know ("${link.kind}"), so the row is labelled rather than guessed.`;
}

export type DeadEndKind = "note" | "feature" | "project";

// The Host spells unresolved targets itself: `EntryLinkProjection.Verbatim` returns
// `feature:<key>` / `project:<key>` for the prefixed kinds and the bare key for a title, with
// the prefix lowercased. Matching on that shape is reading the contract, not re-implementing
// the grammar — no marker here is parsed, only labelled.
export function deadEndKind(target: string): DeadEndKind {
  if (/^feature:/i.test(target)) {
    return "feature";
  }
  return /^project:/i.test(target) ? "project" : "note";
}

export function deadEndLabel(target: string): string {
  return target.replace(/^(feature|project):/i, "").trim() || target;
}

export function deadEndDetail(kind: DeadEndKind): string {
  if (kind === "feature") {
    return "No feature resolved from this marker — the number may be taken on more than one board, or the slug does not exist.";
  }
  if (kind === "project") {
    return "No project resolved from this name — a case-variant pair on the board makes it ambiguous, and the resolver refuses to guess.";
  }
  return "No note carries this title yet.";
}

// Creating a note only ever clears a dead end the marker can land on a title for. Writing a
// note titled `feature:184` would not help: `WikiLinkParser` reads the prefix as a kind
// before it looks at titles, so the marker stays unresolved and the owner gets a second
// dead end with a title that cannot be linked.
export function creatableTitle(target: string): string | null {
  return deadEndKind(target) === "note" ? target.trim() || null : null;
}

export interface RelatedRow {
  id: string;
  title: string;
  snippet: string;
  type: string;
}

export const RELATED_LIMIT = 5;

export function relatedRows(
  hits: readonly { entryId: string; title: string; snippet: string; type: string }[],
  options: { selfId: string; linkedIds?: ReadonlySet<string>; limit?: number },
): RelatedRow[] {
  const limit = options.limit ?? RELATED_LIMIT;
  const rows: RelatedRow[] = [];
  const seen = new Set<string>();
  for (const hit of hits) {
    if (rows.length >= limit) {
      break;
    }
    if (hit.entryId === options.selfId || seen.has(hit.entryId)) {
      continue;
    }
    if (options.linkedIds?.has(hit.entryId)) {
      continue;
    }
    seen.add(hit.entryId);
    rows.push({ id: hit.entryId, title: hit.title, snippet: hit.snippet, type: hit.type });
  }
  return rows;
}

export function linkedEntryIds(incoming: readonly JournalBacklink[], outgoing: readonly JournalOutgoingLink[]): Set<string> {
  const ids = new Set<string>();
  for (const link of outgoing) {
    if (link.kind === "entry") {
      ids.add(link.id);
    }
  }
  for (const backlink of incoming) {
    ids.add(backlink.entryId);
  }
  return ids;
}

// The embedder is asked about the note, not about a keyword the owner has to invent, and the
// query is clamped: a note is prose, and shipping a ten-thousand-character body as a vector
// query costs the box an embedding round trip for no extra signal. Code points, not UTF-16
// units — a cut unit would send a lone surrogate over the wire.
export const RELATED_QUERY_MAX = 500;

export function relatedQuery(title: string, content: string, max: number = RELATED_QUERY_MAX): string {
  const text = [title.trim(), content.trim()].filter((part) => part.length > 0).join("\n");
  const chars = Array.from(text);
  return chars.slice(0, max).join("");
}

export interface AuthorLine {
  label: string;
  agent: boolean;
}

// The boundary ruling made visible (spec R6): a human write carries no agent alias and reads
// as the owner's own, an agent write carries the alias and shows it in mono.
export function authorLine(authorAlias: string | null): AuthorLine {
  const alias = authorAlias?.trim() ?? "";
  return alias.length > 0 ? { label: alias, agent: true } : { label: "you", agent: false };
}
