import { queryOptions } from "@tanstack/react-query";
import { getJournalNote, listJournalNotes } from "@/features/journal/api";
import type { JournalStreamFilters } from "@/features/journal/types";
import type { ProjectResponse } from "@/features/projects";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const JOURNAL_RESERVED_PROJECT_NAME = "Journal";

export const journalKeys = {
  all: ["journal"] as const,
  lists: () => [...journalKeys.all, "list"] as const,
  // filters carries from/to/tag/link — all four stream filters ride the key,
  // so switching one never reuses another combination's cache.
  list: (projectId: string, filters: JournalStreamFilters) =>
    [...journalKeys.lists(), projectId, filters] as const,
  detail: (id: string) => [...journalKeys.all, "detail", id] as const,
};

export const journalStreamQueryOptions = (projectId: string, filters: JournalStreamFilters) =>
  queryOptions({
    queryKey: journalKeys.list(projectId, filters),
    queryFn: () => listJournalNotes(projectId, filters),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const journalNoteQueryOptions = (id: string) =>
  queryOptions({
    queryKey: journalKeys.detail(id),
    queryFn: () => getJournalNote(id),
    enabled: id.length > 0,
  });

export function journalDayWindow(day: string): { from: string; to: string } | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) {
    return null;
  }
  const [year, month, dayOfMonth] = day.split("-").map(Number);
  const from = new Date(year, month - 1, dayOfMonth);
  const to = new Date(year, month - 1, dayOfMonth + 1);
  return { from: from.toISOString(), to: to.toISOString() };
}

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
