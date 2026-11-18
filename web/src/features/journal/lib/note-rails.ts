import type { JournalBacklink, JournalOutgoingLink } from "../types.ts";

export function noteHref(id: string): string {
  return `/journal?note=${encodeURIComponent(id)}`;
}

export function projectHref(id: string): string {
  return `/dashboard?project=${encodeURIComponent(id)}`;
}

export function dayHref(day: string): string {
  return `/journal?day=${encodeURIComponent(day)}`;
}

// The back link goes home with the filters the owner arrived on, minus `note` — losing the
// day or tag chip they had set is the kind of thing that makes a stream feel like it is
// fighting back.
export function streamHrefFrom(search: string): string {
  const params = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  params.delete("note");
  const query = params.toString();
  return query.length > 0 ? `/journal?${query}` : "/journal";
}

export interface OutgoingGroups {
  notes: JournalOutgoingLink[];
  projects: JournalOutgoingLink[];
  features: JournalOutgoingLink[];
}

export function groupOutgoing(links: readonly JournalOutgoingLink[]): OutgoingGroups {
  const groups: OutgoingGroups = { notes: [], projects: [], features: [] };
  for (const link of links) {
    if (link.kind === "entry") {
      groups.notes.push(link);
    } else if (link.kind === "project") {
      groups.projects.push(link);
    } else {
      groups.features.push(link);
    }
  }
  return groups;
}

// A feature row carries its guid and its title — `EntryLinkReader.LoadTargetLabelsAsync`
// labels features with `Title`, so neither the slug nor the owning project survives into the
// DTO, and `/dashboard?project=…&feature=<slug>` cannot be built from what the rail has. The
// row stays honest and unclickable rather than guessing a link; the missing fields are filed
// against the backend, not bolted on here.
export function outgoingHref(link: JournalOutgoingLink): string | null {
  if (link.kind === "entry") {
    return noteHref(link.id);
  }
  return link.kind === "project" ? projectHref(link.id) : null;
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
