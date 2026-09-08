"use client";

import { useQuery } from "@tanstack/react-query";
import { directoryQueryOptions, treeRootsQueryOptions } from "@/features/projects/queries";

export function useTreeRoots() {
  return useQuery(treeRootsQueryOptions());
}

export function useDirectory(path: string | null) {
  return useQuery(directoryQueryOptions(path));
}
