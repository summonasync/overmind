import { keepPreviousData, queryOptions } from "@tanstack/react-query";
import {
  getJournalGraph,
  getJournalNote,
  listJournalNotes,
  listJournalTagCounts,
} from "@/features/journal/api";
import type { GraphViewFilters, JournalServerFilters } from "@/features/journal/types";
import type { ProjectResponse } from "@/features/projects";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const JOURNAL_RESERVED_PROJECT_NAME = "Journal";

export const journalKeys = {
  all: ["journal"] as const,
  lists: () => [...journalKeys.all, "list"] as const,
  // The key is the server's input, not the URL's: tag, link, project and the activity pair
  // (the day filter's wire form) ride the query, so switching one never reuses another
  // combination's cache. The pair is why keying on the URL's `day` string is unnecessary
  // and on its wire form is mandatory — a changed day is a changed answer now that the
  // server owns the cut, and one shared entry would show one day's rows under another
  // day's chip.
  list: (projectId: string, filters: JournalServerFilters) =>
    [...journalKeys.lists(), projectId, filters] as const,
  detail: (id: string) => [...journalKeys.all, "detail", id] as const,
  // The prefix every detail key shares. A save rewrites the rails of notes other than the one
  // being edited — the target that just gained a backlink, the source that lost one, any note
  // whose unresolved marker became a real edge — and the newly linked targets are precisely
  // the ones the pre-save payload could not name. So the surface that shows rails stales the
  // whole shelf, which costs a refetch only for what is mounted. Task 8's rail-keeping fix;
  // see features/journal/components/note-view.tsx.
  detailsRoot: () => [...journalKeys.all, "detail"] as const,
  counts: (projectId: string) => [...journalKeys.all, "counts", projectId] as const,
};

export const journalStreamQueryOptions = (projectId: string, filters: JournalServerFilters) =>
  queryOptions({
    queryKey: journalKeys.list(projectId, filters),
    queryFn: () => listJournalNotes(projectId, filters),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const journalTagCountsQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: journalKeys.counts(projectId),
    queryFn: () => listJournalTagCounts(projectId),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const graphKeys = {
  all: ["journal", "graph"] as const,
  // Every graph filter is the endpoint's own input, so all of them ride the key — the field a
  // widened `includeDone` answers is not the field the default answers, and reusing one cache
  // for both is how a legend starts describing a view nobody asked for.
  field: (filters: GraphViewFilters) => [...graphKeys.all, filters] as const,
};

export const graphQueryOptions = (filters: GraphViewFilters) =>
  queryOptions({
    queryKey: graphKeys.field(filters),
    queryFn: () => getJournalGraph(filters),
    // The field ages with the inbox it maps, so a link an agent wrote while the owner was
    // looking shows up on the same beat the stream rows move.
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

// The card accent reads its answer here: the same endpoint, the same filters, `link`
// forced to the cohort the server defines. No browser-side guess at what "linked" means.
export const journalLinkedCohortQueryOptions = (
  projectId: string,
  filters: JournalServerFilters,
) =>
  queryOptions({
    queryKey: journalKeys.list(projectId, { ...filters, link: "linked" }),
    queryFn: () => listJournalNotes(projectId, { ...filters, link: "linked" }),
    // While the page itself is the linked cohort the subset is that same cache entry, and
    // when the page is the unlinked cohort every row on it is unlinked by the same answer.
    enabled: projectId.length > 0 && filters.link !== "unlinked",
    // A tag or project edit gives the cohort a new key, and with no placeholder the answer
    // is undefined for one round trip — every bar would vanish and repopulate under the
    // reader. The previous cohort colours the previous-looking rows until the new one lands.
    placeholderData: keepPreviousData,
    // Ages with the page it colours, so a link an agent added while the owner was reading
    // moves the accent on the same beat the rows move.
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const journalNoteQueryOptions = (id: string) =>
  queryOptions({
    queryKey: journalKeys.detail(id),
    queryFn: () => getJournalNote(id),
    enabled: id.length > 0,
  });

// <removed 2026-11-18 — journalDayWindow (local midnight → the since/until pair spec R5
// prescribed for the day filter): the endpoint binds that pair to CreatedAt, so sending it
// hides notes touched that day but created earlier — the opposite of the activity-ordered
// inbox. The day partitions locally in lib/stream-filters.ts until the API grows an
// UpdatedAt window.>
// <superseded 2026-11-23 — 249 T2: the UpdatedAt window arrived as
// activitySince/activityUntil, and the local partition ended with it; the day's wire form
// is journalActivityWindow in lib/stream-filters.ts.>

// Mirrors ProjectNameMatch.PickDeterministic on the Host: the reserved container
// lookup is deterministic (exact ordinal name wins, else earliest createdAt among
// case variants), unlike markers which go strict-unresolved on a collision.
export function pickJournalContainerId(projects: ProjectResponse[]): string {
  const variants: ProjectResponse[] = [];
  for (const project of projects) {
    if (project.name === JOURNAL_RESERVED_PROJECT_NAME) {
      return project.id;
    }
    if (project.name.toLowerCase() === JOURNAL_RESERVED_PROJECT_NAME.toLowerCase()) {
      variants.push(project);
    }
  }
  if (variants.length === 0) {
    return "";
  }
  const oldest = [...variants].sort((a, b) => {
    const byCreated = Date.parse(a.createdAt) - Date.parse(b.createdAt);
    if (byCreated !== 0) {
      return byCreated;
    }
    return a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
  })[0];
  return oldest.id;
}
