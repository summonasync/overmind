import type { EntryType } from "@/shared/api/types";

export type JournalLinkKind = "entry" | "feature" | "project";

export interface JournalOutgoingLink {
  kind: JournalLinkKind;
  id: string;
  label: string;
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
// Two doors, and the split is measured rather than stylistic. `tag`, `link` and `project`
// ride the list query and the Host answers them itself — `link` and `project` through
// EntryStreamFilter over EntryLinkTriage. `day` does not ride it: the endpoint's `since`
// and `until` bound CreatedAt (EntryQueryFilters.Apply) while the inbox is ordered by
// UpdatedAt, so a creation-day window would drop the very notes the stream puts on top.
// The day partitions the fetched rows by local activity day instead — see
// lib/stream-filters.ts.
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
export interface JournalServerFilters {
  tag: string;
  link: JournalLinkFilter;
  project: string;
}
