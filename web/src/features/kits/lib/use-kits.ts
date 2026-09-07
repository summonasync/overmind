"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  activateKit,
  addKitEntry,
  createKit,
  deactivateKit,
  deleteKit,
  removeKitEntry,
  updateKit,
} from "@/features/kits/api";
import { driftQueryOptions, kitKeys, kitListQueryOptions } from "@/features/kits/queries";
import type { KitFormValues } from "@/features/kits/types";

export function useKitList() {
  const query = useQuery(kitListQueryOptions());
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function useDrift(projectId: string, enabled: boolean) {
  const query = useQuery({ ...driftQueryOptions(projectId), enabled: enabled && projectId.length > 0 });
  return { data: query.data ?? null, loading: query.isPending };
}

export function useKitMutations() {
  const queryClient = useQueryClient();

  const invalidateKits = () => queryClient.invalidateQueries({ queryKey: kitKeys.lists() });
  const invalidateDrift = () => queryClient.invalidateQueries({ queryKey: kitKeys.drifts() });

  const create = useMutation({
    mutationFn: (values: KitFormValues) => createKit(values),
    onSuccess: invalidateKits,
  });

  const update = useMutation({
    mutationFn: ({ id, values }: { id: string; values: KitFormValues }) => updateKit(id, values),
    onSuccess: invalidateKits,
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteKit(id),
    onSuccess: invalidateKits,
  });

  const addEntry = useMutation({
    mutationFn: ({ kitId, entryId }: { kitId: string; entryId: string }) => addKitEntry(kitId, entryId),
    onSuccess: invalidateKits,
  });

  const removeEntry = useMutation({
    mutationFn: ({ kitId, entryId }: { kitId: string; entryId: string }) => removeKitEntry(kitId, entryId),
    onSuccess: invalidateKits,
  });

  const activate = useMutation({
    mutationFn: ({ projectId, kitId }: { projectId: string; kitId: string }) =>
      activateKit(projectId, kitId),
    onSuccess: () => {
      invalidateKits();
      invalidateDrift();
    },
  });

  const deactivate = useMutation({
    mutationFn: ({ projectId, kitId }: { projectId: string; kitId: string }) =>
      deactivateKit(projectId, kitId),
    onSuccess: () => {
      invalidateKits();
      invalidateDrift();
    },
  });

  return { create, update, remove, addEntry, removeEntry, activate, deactivate };
}
