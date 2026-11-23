"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo } from "react";
import {
  createJournalNote,
  deleteJournalNote,
  updateJournalNote,
} from "@/features/journal/api";
import {
  journalKeys,
  journalLinkedCohortQueryOptions,
  journalNoteQueryOptions,
  journalStreamQueryOptions,
  journalTagCountsQueryOptions,
  pickJournalContainerId,
} from "@/features/journal/queries";
import type {
  JournalLinkAccent,
  JournalNote,
  JournalNoteFormValues,
  JournalServerFilters,
} from "@/features/journal/types";
import { entryTagsQueryOptions } from "@/features/entries";
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

export function useJournalStream(projectId: string, filters: JournalServerFilters) {
  const query = useQuery(journalStreamQueryOptions(projectId, filters));
  return {
    data: query.data ?? [],
    // A gated query reports pending forever; with no container there is nothing to
    // load, and the caller must reach its empty state instead of a permanent skeleton.
    loading: query.isPending && projectId.length > 0,
    error: query.error,
    refetch: query.refetch,
  };
}

// Rail numbers live on the same live-sync cadence as the stream: a tag added through the
// editor must not leave the rail counting a world the list no longer shows.
export function useJournalTagCounts(projectId: string) {
  const query = useQuery(journalTagCountsQueryOptions(projectId));
  return {
    counts: query.data ?? [],
    loading: query.isPending && projectId.length > 0,
    error: query.error,
  };
}

// Which cards get the linked accent, answered by the server's link cohort rather than by a
// browser-side reading of the rails — list rows carry no rail data (T3), so the client
// could not answer this honestly even if it tried.
export function useJournalLinkAccents(
  projectId: string,
  filters: JournalServerFilters,
  rows: readonly JournalNote[],
) {
  const cohort = useQuery(journalLinkedCohortQueryOptions(projectId, filters));

  const linked = useMemo<Set<string> | null>(() => {
    if (filters.link === "unlinked") {
      return new Set();
    }
    if (filters.link === "linked") {
      return new Set(rows.map((row) => row.id));
    }
    return cohort.data ? new Set(cohort.data.map((row) => row.id)) : null;
  }, [cohort.data, filters.link, rows]);

  return {
    accentOf: (noteId: string): JournalLinkAccent => {
      if (linked === null) {
        return null;
      }
      return linked.has(noteId) ? "linked" : "unsorted";
    },
    // A cohort request that died must not cost the reader its accents forever: the stream
    // stays usable, the bars just stay off, same degraded rule as the tag colours.
    cohortFailed: cohort.isError,
  };
}

// Tag colours come from the entries feature's registry — the same table the entries list
// paints, and the same cache entry, so a colour set once colours both surfaces. An empty
// registry is a normal state (no tag has been coloured yet), not a degraded one.
export function useTagColorMap(): ReadonlyMap<string, string | null> {
  const tags = useQuery(entryTagsQueryOptions());
  return useMemo(
    () => new Map(tags.data?.map((tag) => [tag.name, tag.color]) ?? []),
    [tags.data],
  );
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
