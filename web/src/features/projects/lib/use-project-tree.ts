"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { addTreeRoot, removeTreeRoot } from "@/shared/api/system";
import {
  browseRootsQueryOptions,
  directoryQueryOptions,
  projectKeys,
  treeRootsQueryOptions,
} from "@/features/projects/queries";

export function useTreeRoots() {
  return useQuery(treeRootsQueryOptions());
}

export function useBrowseRoots() {
  return useQuery(browseRootsQueryOptions());
}

export function useDirectory(path: string | null) {
  return useQuery(directoryQueryOptions(path));
}

export function useAddTreeRoot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: addTreeRoot,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.tree("roots") }),
  });
}

export function useRemoveTreeRoot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: removeTreeRoot,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.tree("roots") }),
  });
}
