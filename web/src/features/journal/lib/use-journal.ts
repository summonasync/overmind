"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createJournalNote,
  deleteJournalNote,
  updateJournalNote,
} from "@/features/journal/api";
import {
  journalKeys,
  journalNoteQueryOptions,
  journalStreamQueryOptions,
  pickJournalContainerId,
} from "@/features/journal/queries";
import type { JournalNoteFormValues, JournalStreamFilters } from "@/features/journal/types";
import { projectKeys, projectListQueryOptions } from "@/features/projects";

// Journal rows live in the reserved "Journal" project (spec R2); there is no
// dedicated endpoint for its id, so the container is resolved from the shared
// projects cache with the same deterministic pick the Host applies.
export function useJournalContainer() {
  const query = useQuery(projectListQueryOptions());
  return {
    containerId: pickJournalContainerId(query.data ?? []),
    loading: query.isPending,
    error: query.error,
  };
}

export function useJournalStream(projectId: string, filters: JournalStreamFilters) {
  const query = useQuery(journalStreamQueryOptions(projectId, filters));
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function useJournalNote(id: string) {
  return useQuery(journalNoteQueryOptions(id));
}

export function useJournalMutations(projectId: string) {
  const queryClient = useQueryClient();

  const invalidateStream = () =>
    queryClient.invalidateQueries({ queryKey: journalKeys.lists() });

  const create = useMutation({
    mutationFn: (values: JournalNoteFormValues) => createJournalNote(projectId, values),
    onSuccess: () => {
      invalidateStream();
      // A capture is what births the reserved container on the Host side, so the
      // projects list is stale the moment the first note lands: without this the
      // note saves while every container consumer (the rail, the pickers, and the
      // useJournalContainer lookup above) keeps reporting that there is no journal.
      // Lists only — a project that did not exist a second ago has no detail or
      // settings entry to stale out, and the tree caches folder browsing.
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });

  // The note view reads its rails (outgoing/incoming/unresolved) off detail(id),
  // which the update response does not refresh on its own — both caches must be
  // invalidated or the rails keep the pre-save edges.
  const update = useMutation({
    mutationFn: ({ id, values }: { id: string; values: JournalNoteFormValues }) =>
      updateJournalNote(id, values),
    onSuccess: (saved) => {
      invalidateStream();
      queryClient.invalidateQueries({ queryKey: journalKeys.detail(saved.id) });
    },
  });

  const remove = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      deleteJournalNote(id, reason),
    onSuccess: (_deleted, { id }) => {
      invalidateStream();
      queryClient.removeQueries({ queryKey: journalKeys.detail(id) });
    },
  });

  return { create, update, remove };
}
