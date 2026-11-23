// The graph surface is deliberately NOT re-exported here: this barrel is what every journal
// consumer imports, and `components/graph-canvas` pulls d3-force behind it. Re-exporting the
// island would put the simulation in every route's bundle, which R3's acceptance row measures.
// The graph page reaches its own feature by path — the same way projects-page reaches
// `features/projects/lib/project-order`. A pin in `web/tests/journal-graph.test.mts` guards it.

export { CaptureOverlay } from "./components/capture-overlay";
export { JournalCard } from "./components/journal-card";
export { JournalStream } from "./components/journal-stream";
export { JournalToolbar } from "./components/journal-toolbar";
export { NoteView } from "./components/note-view";
export { TagRail } from "./components/tag-rail";
export {
  EMPTY_JOURNAL_FILTERS,
  JOURNAL_FILTER_KEYS,
  formatJournalStamp,
  isJournalFilterActive,
  journalActivityWindow,
  journalServerFilters,
  localDayOf,
  parseJournalFilters,
  sortByActivityDesc,
  writeJournalFilters,
} from "./lib/stream-filters";
export { CaptureOpenProvider, useOpenCapture } from "./lib/capture-open-context";
export { useCaptureHotkeys } from "./lib/use-capture-hotkeys";
export type { JournalActivityWindow } from "./lib/stream-filters";
export { useJournalFilters } from "./lib/use-journal-filters";
export {
  useJournalContainer,
  useJournalLinkAccents,
  useJournalMutations,
  useJournalNote,
  useJournalStream,
  useTagColorMap,
} from "./lib/use-journal";
export {
  JOURNAL_RESERVED_PROJECT_NAME,
  journalKeys,
  journalLinkedCohortQueryOptions,
  journalNoteQueryOptions,
  journalStreamQueryOptions,
  pickJournalContainerId,
} from "./queries";
export type {
  GraphCounts,
  GraphEdge,
  GraphEntryType,
  GraphFeatureStatus,
  GraphField,
  GraphViewFilters,
  GraphNode,
  GraphNodeKind,
  GraphNodeState,
  GraphPosition,
  JournalBacklink,
  JournalLinkAccent,
  JournalLinkFilter,
  JournalLinkKind,
  JournalNote,
  JournalNoteFormValues,
  JournalNoteState,
  JournalOutgoingLink,
  JournalServerFilters,
  JournalStreamFilters,
} from "./types";
