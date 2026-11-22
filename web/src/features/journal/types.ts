import type { EntryType } from "@/shared/api/types";

// The rail's outgoing-link vocabulary — the four target kinds the Host can put on the wire as
// `outgoing[].kind`. It is a mirror of `EntryLinkKind` serialised by the Host's camelCase enum
// converter (`DependencyInjection.cs`, `ConfigureHttpJsonOptions` →
// `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`), so the members are spelled as they
// arrive: `"entry"`, not `"Entry"`.
//
// `task` is the fourth member and it arrived with feature 237's promotion edge: triage points a
// note at a board task, and a rail that had no arm for it filed the row under features. This is
// NOT the graph's node vocabulary — see `GraphNodeKind`, which deliberately cannot express `task`.
export type JournalLinkKind = "entry" | "feature" | "project" | "task";

export interface JournalOutgoingLink {
  kind: JournalLinkKind;
  id: string;
  label: string;
  // Card-address halves the Host resolves for feature and task rows (T10's TargetLabel):
  // features carry their own slug and project, tasks their parent card's. Absent on rows
  // the reader could not resolve, and on entry/project rows the rail addresses directly.
  slug?: string | null;
  projectId?: string | null;
}

export interface JournalBacklink {
  entryId: string;
  title: string;
  snippet: string;
}

export type JournalNoteState = "active" | "lifted" | "superseded";

// The server always serializes the three rail fields; only GET /entries/{id} fills
// them — list, create, update and delete responses carry them empty (measured, T3).
// The consequence for the inbox: a stream row cannot say whether it is linked, so the
// card accent asks the server the same question the chip filters on rather than
// deriving it in the browser.
export interface JournalNote {
  id: string;
  projectId: string;
  type: EntryType;
  title: string;
  content: string;
  currentVersion: number;
  createdAt: string;
  updatedAt: string;
  tags: string[];
  authorAlias: string | null;
  state: JournalNoteState;
  outgoing: JournalOutgoingLink[];
  incoming: JournalBacklink[];
  unresolved: string[];
}

export interface JournalNoteFormValues {
  title: string;
  content: string;
  tags: string[];
}

// The URL grammar of the inbox, one field per parameter, "" meaning unset.
//
// `tag`, `link` and `project` ride the list query and the Host answers them itself —
// `link` and `project` through EntryStreamFilter over EntryLinkTriage. `day` rides it
// too, in its wire form: journalServerFilters turns the day's local-midnight boundaries
// into the UTC activitySince/activityUntil pair the endpoint binds to UpdatedAt
// (EntryQueryFilters.Apply) — the server owns the day cut, the browser drops no row.
// lib/stream-filters.ts holds the grammar and the conversion.
export interface JournalStreamFilters {
  day: string;
  tag: string;
  link: JournalLinkFilter;
  project: string;
}

// Exactly the vocabulary the server validates; anything else answers 400 UnknownLinkFilter.
export type JournalLinkFilter = "" | "linked" | "unlinked";

// The card's left accent bar: triage state told in colour instead of in text. `null` is a
// real state — the server has not answered the link question yet, or its answer died — and
// renders the plain border rather than a guess.
export type JournalLinkAccent = "unsorted" | "linked" | null;

// The subset that reaches the server, and therefore the subset the cache key carries.
// The activity pair is the day filter's wire form: UTC ISO instants derived from the
// selected local day, "" meaning absent so the server's default window applies. The pair
// travels as one unit — journalServerFilters derives both halves or neither — and a
// changed day is a changed server input, so it changes the cache key with it.
export interface JournalServerFilters {
  tag: string;
  link: JournalLinkFilter;
  project: string;
  activitySince: string;
  activityUntil: string;
}

// ── The graph field ────────────────────────────────────────────────────────────────────
//
// Two vocabularies live in this slice and they are NOT the same union (review-237-03 m6).
// `JournalLinkKind` above is the rail's four target kinds. A graph NODE is one of three:
// `task` is an edge target the promotion door writes, never a dot the canvas can draw —
// `GraphQueryFilter.ValidateFocus` refuses `focus=task:*` and `GraphReader.BuildEdgesAsync`
// drops any edge whose endpoint is not in the node set. Reusing `JournalLinkKind` here would
// make the payload lie in the other direction and ask the renderer for a shape that cannot
// exist, so the narrower union is the type and `lib/graph-payload.ts` refuses a fourth kind
// at the door instead of drawing it.
//
// Every member is spelled the way the wire spells it: the Host registers
// `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` in `DependencyInjection.cs`
// (`ConfigureHttpJsonOptions`), so `kind` arrives `"entry"`, never `"Entry"`. The switch arms
// in `lib/graph-payload.ts` are the executable statement of that posture.
export type GraphNodeKind = "entry" | "project" | "feature";

// R1's node universe: Journal, Decision and Idea entries. Audit/Note/Skill/Tombstone/Gate and
// Guard rows are not nodes in v1, and `GraphQueryFilter.ValidateEntryTypes` 400s anything else.
export type GraphEntryType = "journal" | "decision" | "idea";

export type GraphFeatureStatus = "draft" | "ready" | "inProgress" | "review" | "done";

export interface GraphNode {
  kind: GraphNodeKind;
  id: string;
  label: string;
  projectId: string;
  entryType: GraphEntryType | null;
  featureStatus: GraphFeatureStatus | null;
  // True means this row carries no link anywhere; `lib/graph-payload.ts` derives the third
  // state (linked, but with every link leaving this view) from this flag plus the edge set.
  orphan: boolean;
}

export type GraphNodeState = "linked" | "orphan" | "offView";

export interface GraphEdge {
  fromKind: GraphNodeKind;
  fromId: string;
  toKind: GraphNodeKind;
  toId: string;
  hasManual: boolean;
}

export interface GraphPosition {
  kind: GraphNodeKind;
  id: string;
  x: number;
  y: number;
}

// One row the canvas sends when a drag settles (pinned: true) or the reset control lifts pins
// (pinned: false). The server, not the browser, is the only place these live — R5 forbids a
// client-side copy, which is why this type has no storage besides the door.
export interface GraphPositionWrite {
  kind: GraphNodeKind;
  id: string;
  x: number;
  y: number;
  pinned: boolean;
}

// `isolatedInView` is a NODE count, not a link count: the dropped-edge tally is not
// reconstructible from this payload (`GraphContracts.cs` says so), so the legend may say "N
// notes here link to something outside this view" and must never say "N links lead outside".
export interface GraphCounts {
  nodes: number;
  edges: number;
  orphans: number;
  entryNodes: number;
  projectNodes: number;
  featureNodes: number;
  isolatedInView: number;
}

export interface GraphField {
  nodes: GraphNode[];
  edges: GraphEdge[];
  positions: GraphPosition[];
  counts: GraphCounts;
  // Rows the wire carried that this build cannot draw, counted rather than dropped silently —
  // an empty array is the normal case, a non-empty one says the endpoint grew past the union.
  unsupported: { nodes: number; edges: number };
}

// ── Triage: the note becoming board work ───────────────────────────────────────────────
//
// `NotePromotionResult` is `NotePromotionResponse` on the wire — the door's whole answer, in the
// fields the surface can act on. `featureSlug` is what the board cache addresses a card by, and
// `targetKind`/`targetId` name the artifact the provenance edge points at, which for a task is the
// task and not its parent.
export interface NotePromotionResult {
  entryId: string;
  targetKind: PromotionTargetKind;
  targetId: string;
  featureId: string;
  featureSlug: string;
}

// The two targets the owner can sort a thought into, spelled as the wire spells them. Narrower
// than `JournalLinkKind` on purpose: `entry` and `project` are what a note links to by writing a
// marker, never what a promotion mints.
export type PromotionTargetKind = "feature" | "task";

// The form state, as opposed to the door's body: a task names itself with a brief and carries the
// contract in the body, a feature names itself with a title, and both need a board to land on.
export interface PromotionForm {
  target: PromotionTargetKind;
  projectId: string;
  featureId: string;
  name: string;
  body: string;
}

export interface FeatureCandidate {
  id: string;
  label: string;
  done: boolean;
}

// Every field travels, so one shape serves the URL and the request — the same posture the
// stream's server filters hold now that the day is among them as the activity pair: the
// graph's filters are all the endpoint's own. `lib/graph-filters.ts` reads and writes it,
// `lib/graph-query.ts` sends it. `depth` stays a string because the URL keeps the untyped
// text — an empty depth means "the endpoint's own default of one hop", and coercing it to 0
// client-side would ask for nothing.
export interface GraphViewFilters {
  types: GraphEntryType[];
  project: string;
  focus: string;
  depth: string;
  orphansOnly: boolean;
  includeDone: boolean;
  search: string;
}
