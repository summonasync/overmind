import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import { journalStreamPath } from "@/features/journal/lib/stream-query";
import type {
  JournalNote,
  JournalNoteFormValues,
  JournalServerFilters,
} from "@/features/journal/types";

export function listJournalNotes(
  projectId: string,
  filters: JournalServerFilters,
): Promise<JournalNote[]> {
  return apiGet<JournalNote[]>(journalStreamPath(projectId, filters));
}

export function getJournalNote(id: string): Promise<JournalNote> {
  return apiGet<JournalNote>(`/entries/${id}`);
}

export function createJournalNote(
  projectId: string,
  values: JournalNoteFormValues,
): Promise<JournalNote> {
  return apiPost<JournalNote>("/entries", {
    // An empty id is not a missing argument, it is the "no forced destination"
    // signal: the Host resolves the reserved container and creates it when the
    // very first thought is what brings it into existence.
    projectId: projectId.length > 0 ? projectId : null,
    type: "journal",
    title: values.title,
    content: values.content,
    tags: values.tags,
    skillFiles: null,
  });
}

export function updateJournalNote(
  id: string,
  values: JournalNoteFormValues,
): Promise<JournalNote> {
  return apiPut<JournalNote>(`/entries/${id}`, {
    title: values.title,
    content: values.content,
    tags: values.tags,
    skillFiles: null,
  });
}

export function deleteJournalNote(id: string, reason?: string): Promise<JournalNote | undefined> {
  const trimmed = reason?.trim();
  const query = trimmed ? `?reason=${encodeURIComponent(trimmed)}` : "";
  return apiDelete<JournalNote | undefined>(`/entries/${id}${query}`);
}
