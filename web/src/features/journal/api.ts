import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import { normalizeGraphField } from "@/features/journal/lib/graph-payload";
import { graphPath } from "@/features/journal/lib/graph-query";
import { journalStreamPath } from "@/features/journal/lib/stream-query";
import type {
  GraphField,
  GraphViewFilters,
  JournalNote,
  JournalNoteFormValues,
  JournalServerFilters,
  NotePromotionResult,
} from "@/features/journal/types";
import { type PromotionRequest } from "@/features/journal/lib/note-promotion";

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

// One request for the whole field (R1): nodes, edges, positions and counts in a single payload,
// so the canvas never issues a per-node follow-up to draw a link. The wire is normalised on the
// way in rather than inspected at every draw site — that is where "Task is never a node" stops
// being a comment and becomes a rule.
export async function getJournalGraph(filters: GraphViewFilters): Promise<GraphField> {
  const raw: unknown = await apiGet<unknown>(graphPath(filters));
  return normalizeGraphField(raw);
}

// The triage door, on the human prefix (spec R8: promoting one of the owner's thoughts is the
// owner's act). One POST answers with the artifact it minted and the card to invalidate; the door
// writes no note and copies no text, so there is nothing to pre-save here.
//
// The body is the door's own shape: `{projectId,title,body,priority?}` for a feature,
// `{featureId,body,brief?}` for a task. Priority is deliberately absent — the door's default is the
// P2 every un-prioritised card starts on, and the surface that asks for a priority is a later card.
export function promoteNote(entryId: string, request: PromotionRequest): Promise<NotePromotionResult> {
  const path = `/entries/${encodeURIComponent(entryId)}/promote-to-${request.target}`;
  const body =
    request.target === "feature"
      ? { projectId: request.projectId, title: request.title, body: request.body }
      : { featureId: request.featureId, body: request.body, brief: request.brief };

  return apiPost<NotePromotionResult>(path, body);
}
