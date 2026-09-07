"use client";

import { useQuery } from "@tanstack/react-query";
import { searchQueryOptions } from "@/features/search/queries";
import type { SearchMode } from "@/features/search/types";

export function useSearch(query: string, mode: SearchMode, projectId: string) {
  const searchQuery = useQuery(searchQueryOptions(query, mode, projectId));
  return {
    data: searchQuery.data ?? null,
    loading: searchQuery.isPending && query.trim().length > 0,
    error: searchQuery.error ?? null,
  };
}
