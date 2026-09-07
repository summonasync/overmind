import { queryOptions } from "@tanstack/react-query";
import { searchEntries } from "@/features/search/api";
import type { SearchMode } from "@/features/search/types";

export const searchKeys = {
  all: ["search"] as const,
  results: (query: string, mode: SearchMode, projectId: string) =>
    [...searchKeys.all, "results", mode, projectId, query] as const,
};

export const searchQueryOptions = (query: string, mode: SearchMode, projectId: string) =>
  queryOptions({
    queryKey: searchKeys.results(query, mode, projectId),
    queryFn: () => searchEntries(query, mode, projectId || undefined),
    enabled: query.trim().length > 0,
    retry: false,
  });
