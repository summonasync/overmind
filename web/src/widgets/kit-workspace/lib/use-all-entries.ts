"use client";

import { useQueries } from "@tanstack/react-query";
import type { EntryResponse } from "@/features/entries";
import { entryListQueryOptions } from "@/features/entries";
import { useProjectList } from "@/features/projects";

export function useAllEntries(): Record<string, EntryResponse> {
  const { data: projects } = useProjectList();

  const queries = useQueries({
    queries: projects.map((project) => entryListQueryOptions(project.id, { type: "", tag: "" })),
  });

  const map: Record<string, EntryResponse> = {};
  for (const query of queries) {
    for (const entry of query.data ?? []) {
      map[entry.id] = entry;
    }
  }
  return map;
}
