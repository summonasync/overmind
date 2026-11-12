import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type { EntryType } from "@/shared/api/types";
import type {
  EntryFormValues,
  EntryListFilters,
  EntryResponse,
  EntryVersionResponse,
  TagResponse,
} from "@/features/entries/types";

function resolveUntilExclusive(until: string): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(until)) {
    return until;
  }
  const [year, month, day] = until.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day + 1)).toISOString().slice(0, 10);
}

function buildListQuery(projectId: string, filters: EntryListFilters): string {
  const params = new URLSearchParams();
  if (filters.type) {
    params.set("type", filters.type);
  }
  if (filters.tag.trim()) {
    params.set("tag", filters.tag.trim());
  }
  params.set("state", filters.state ?? "all");
  if (filters.since) {
    params.set("since", filters.since);
  }
  if (filters.until) {
    params.set("until", resolveUntilExclusive(filters.until));
  }
  const author = filters.author?.trim();
  if (author) {
    params.set("author", author);
  }
  return `/projects/${projectId}/entries?${params.toString()}`;
}

export function listEntries(
  projectId: string,
  filters: EntryListFilters,
): Promise<EntryResponse[]> {
  return apiGet<EntryResponse[]>(buildListQuery(projectId, filters));
}

export function getEntry(id: string): Promise<EntryResponse> {
  return apiGet<EntryResponse>(`/entries/${id}`);
}

export function listEntryVersions(id: string): Promise<EntryVersionResponse[]> {
  return apiGet<EntryVersionResponse[]>(`/entries/${id}/versions`);
}

export function createEntry(
  projectId: string,
  type: EntryType,
  values: EntryFormValues,
): Promise<EntryResponse> {
  return apiPost<EntryResponse>("/entries", {
    projectId,
    type,
    title: values.title,
    content: values.content,
    tags: values.tags,
    skillFiles: values.type === "skill" ? values.skillFiles : null,
  });
}

export function updateEntry(id: string, values: EntryFormValues): Promise<EntryResponse> {
  return apiPut<EntryResponse>(`/entries/${id}`, {
    title: values.title,
    content: values.content,
    tags: values.tags,
    skillFiles: values.type === "skill" ? values.skillFiles : null,
  });
}

export function deleteEntry(id: string, reason?: string): Promise<EntryResponse | undefined> {
  const trimmed = reason?.trim();
  const query = trimmed ? `?reason=${encodeURIComponent(trimmed)}` : "";
  return apiDelete<EntryResponse | undefined>(`/entries/${id}${query}`);
}

export function listTags(): Promise<TagResponse[]> {
  return apiGet<TagResponse[]>("/tags");
}

export function updateTagColor(id: string, color: string): Promise<TagResponse> {
  return apiPut<TagResponse>(`/tags/${id}`, { color });
}
