import { apiGet } from "@/shared/api/client";
import type { SearchMode, SearchResponse } from "@/features/search/types";

export function searchEntries(
  query: string,
  mode: SearchMode,
  projectId?: string,
): Promise<SearchResponse> {
  const params = new URLSearchParams({ q: query, mode });
  if (projectId) {
    params.set("projectId", projectId);
  }
  return apiGet<SearchResponse>(`/search?${params.toString()}`);
}
