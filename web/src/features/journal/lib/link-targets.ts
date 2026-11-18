import { entryMarker, featureMarker, projectMarker } from "./note-links.ts";

// What the `[[` list is allowed to offer. The rule this whole module serves is one line of
// the task contract: a suggestion must be a target the Host resolver can actually land on,
// because every proposal rendered as a plain link that saves into an unresolved marker is a
// lie the owner only discovers in the dead-end list. So each arm below carries the reason
// its rows are resolvable, and drops the rows where that reason dies.
export type SuggestionKind = "note" | "project" | "feature" | "create";

export interface LinkSuggestion {
  key: string;
  kind: SuggestionKind;
  label: string;
  detail: string;
  marker: string;
  createsNote: boolean;
}

export interface NoteCandidate {
  id: string;
  title: string;
}

export interface ProjectCandidate {
  id: string;
  name: string;
}

export interface FeatureCandidate {
  projectId: string;
  projectName: string;
  slug: string;
  title: string;
}

export interface CandidateSet {
  notes: readonly NoteCandidate[];
  projects: readonly ProjectCandidate[];
  features: readonly FeatureCandidate[];
}

export const EMPTY_CANDIDATES: CandidateSet = { notes: [], projects: [], features: [] };

export interface SuggestionLimits {
  notes: number;
  projects: number;
  features: number;
}

export const DEFAULT_LIMITS: SuggestionLimits = { notes: 6, projects: 4, features: 4 };

const RESERVED_PREFIX = /^(feature|project):/i;

export function isSuggested(query: string, label: string): boolean {
  if (query.length === 0) {
    return true;
  }
  return label.toLowerCase().includes(query.toLowerCase());
}

// `WikiLinkParser.ToTarget` reads a leading `feature:`/`project:` as a kind marker before it
// ever considers the title branch, so a note titled `feature:184` is a title nothing can
// point at with `[[feature:184]]`. Offering it would put a proposal on screen that saves as
// a dead end.
export function isBareTitleLinkable(title: string): boolean {
  const trimmed = title.trim();
  return trimmed.length > 0 && !RESERVED_PREFIX.test(trimmed);
}

export function buildLinkSuggestions(
  candidates: CandidateSet,
  options: { query: string; currentNoteId: string; limits?: Partial<SuggestionLimits> },
): LinkSuggestion[] {
  const limits = { ...DEFAULT_LIMITS, ...(options.limits ?? {}) };
  const query = options.query.trim();
  const rows: LinkSuggestion[] = [];

  // Exact title first, then the substring hits in the order the stream keeps them (newest
  // activity). Without the ranking, typing a whole title can still land under the cap and
  // offer a create for a note that exists.
  const noteMatches = candidates.notes.filter(
    (note) => note.id !== options.currentNoteId && isBareTitleLinkable(note.title) && isSuggested(query, note.title),
  );
  noteMatches.sort((a, b) => rank(a.title, query) - rank(b.title, query));
  for (const note of noteMatches.slice(0, limits.notes)) {
    rows.push({
      key: `note:${note.id}`,
      kind: "note",
      label: note.title.trim(),
      detail: "note",
      marker: entryMarker(note.title),
      createsNote: false,
    });
  }

  for (const project of candidates.projects) {
    if (rows.filter((row) => row.kind === "project").length >= limits.projects) {
      break;
    }
    if (!isSuggested(query, project.name)) {
      continue;
    }
    rows.push({
      key: `project:${project.id}`,
      kind: "project",
      // The stored spelling, verbatim: an ordinal-case match wins the resolver's collision
      // rule outright, so this is the only casing that cannot come back unresolved.
      label: project.name,
      detail: "project",
      marker: projectMarker(project.name),
      createsNote: false,
    });
  }

  // Exact-slug resolution needs exactly one row on the whole board set; two projects with a
  // borrowed slug make the Host answer unresolved, so neither of them gets offered.
  const slugCount = new Map<string, number>();
  for (const feature of candidates.features) {
    if (feature.slug.trim().length === 0) {
      continue;
    }
    slugCount.set(feature.slug, (slugCount.get(feature.slug) ?? 0) + 1);
  }

  let featureRows = 0;
  for (const feature of candidates.features) {
    if (featureRows >= limits.features) {
      break;
    }
    if ((slugCount.get(feature.slug) ?? 0) > 1) {
      continue;
    }
    if (!isSuggested(query, feature.title) && !isSuggested(query, feature.slug)) {
      continue;
    }
    featureRows += 1;
    rows.push({
      key: `feature:${feature.projectId}:${feature.slug}`,
      kind: "feature",
      label: feature.title.trim().length > 0 ? feature.title : feature.slug,
      detail: feature.projectName.length > 0 ? feature.projectName : "feature",
      marker: featureMarker(feature.slug),
      createsNote: false,
    });
  }

  // Nothing on screen resolves to a note with these exact words? Then the words are a note
  // the journal does not have yet — say it out loud instead of closing the list. The row is
  // labelled as a creation, never as a link, which is what the contract's "does not invent
  // edges" clause asks for.
  if (query.length > 0 && !rows.some((row) => row.kind === "note" && sameTitle(row.label, query))) {
    const created = createRow(query);
    if (created !== null) {
      rows.push(created);
    }
  }

  return rows;
}

function sameTitle(left: string, right: string): boolean {
  return left.trim().toLowerCase() === right.trim().toLowerCase();
}

// sort() is stable, so anything sharing a rank keeps the order the source list arrived in.
function rank(label: string, query: string): number {
  if (query.length === 0) {
    return 0;
  }
  const needle = query.toLowerCase();
  const haystack = label.trim().toLowerCase();
  if (haystack === needle) {
    return 0;
  }
  return haystack.startsWith(needle) ? 1 : 2;
}

function createRow(query: string): LinkSuggestion | null {
  const trimmed = query.trim();
  // A `feature:`/`project:` query can only resolve against its own kind, and a note titled
  // `feature:184` is unreachable by that marker, so there is nothing honest to create. Brackets
  // would break the marker apart on insert.
  if (RESERVED_PREFIX.test(trimmed) || trimmed.includes("[") || trimmed.includes("]")) {
    return null;
  }
  return {
    key: `create:${trimmed.toLowerCase()}`,
    kind: "create",
    label: trimmed,
    detail: "creates a new note",
    marker: entryMarker(trimmed),
    createsNote: true,
  };
}
