"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { EntryType } from "@/shared/api/types";
import { createEntry, deleteEntry, updateEntry } from "@/features/entries/api";
import { entryKeys, entryListQueryOptions } from "@/features/entries/queries";
import type { EntryFormValues, EntryListFilters } from "@/features/entries/types";

export function useEntryList(projectId: string, filters: EntryListFilters) {
  const query = useQuery(entryListQueryOptions(projectId, filters));
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function useEntryMutations(projectId: string) {
  const queryClient = useQueryClient();

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: entryKeys.lists() });

  const create = useMutation({
    mutationFn: ({ type, values }: { type: EntryType; values: EntryFormValues }) =>
      createEntry(projectId, type, values),
    onSuccess: invalidate,
  });

  const update = useMutation({
    mutationFn: ({ id, values }: { id: string; values: EntryFormValues }) =>
      updateEntry(id, values),
    onSuccess: invalidate,
  });

  const remove = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) => deleteEntry(id, reason),
    onSuccess: invalidate,
  });

  return { create, update, remove };
}
