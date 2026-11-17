import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type { JournalNote, JournalNoteFormValues, JournalStreamFilters } from "@/features/journal/types";

function buildStreamQuery(projectId: string, filters: JournalStreamFilters): string {
  const params = new URLSearchParams();
  params.set("type", "journal");
  // The server default for journal is state=all; the inbox must not surface
  // tombstones or superseded rows, so the stream asks for active explicitly.
  params.set("state", "active");
  const tag = filters.tag.trim();
  if (tag.length > 0) {
    params.set("tag", tag);
  }
  if (filters.from.length > 0) {
    params.set("since", filters.from);
  }
  if (filters.to.length > 0) {
    params.set("until", filters.to);
  }
  return `/projects/${projectId}/entries?${params.toString()}`;
}

export function listJournalNotes(
  projectId: string,
  filters: JournalStreamFilters,
): Promise<JournalNote[]> {
  return apiGet<JournalNote[]>(buildStreamQuery(projectId, filters));
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
