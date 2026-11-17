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

// Every field is "" when unset so the query key stays a uniform shape. `link`
// and `project` never reach the server — the entries list has no such
// parameters (and its rows carry empty link fields), the stream filters them
// client-side. `project` is the LINKED-project filter, not an owning-project
// scope: journal rows all live in the reserved container and per-project
// journals are spec anti-scope R2.
export interface JournalStreamFilters {
  from: string;
  to: string;
  tag: string;
  link: string;
  project: string;
}

export const EMPTY_JOURNAL_FILTERS: JournalStreamFilters = {
  from: "",
  to: "",
  tag: "",
  link: "",
  project: "",
};
