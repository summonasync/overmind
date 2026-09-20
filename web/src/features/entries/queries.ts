import { queryOptions } from "@tanstack/react-query";
import { listEntries, listEntryVersions, listTags } from "@/features/entries/api";
import type { EntryListFilters } from "@/features/entries/types";

export const entryKeys = {
  all: ["entries"] as const,
  lists: () => [...entryKeys.all, "list"] as const,
  list: (projectId: string, filters: EntryListFilters) =>
    [...entryKeys.lists(), projectId, filters] as const,
  tags: () => [...entryKeys.all, "tags"] as const,
  detail: (id: string) => [...entryKeys.all, "detail", id] as const,
  versions: (id: string) => [...entryKeys.detail(id), "versions"] as const,
};

export const entryListQueryOptions = (projectId: string, filters: EntryListFilters) =>
  queryOptions({
    queryKey: entryKeys.list(projectId, filters),
    queryFn: () => listEntries(projectId, filters),
    enabled: projectId.length > 0,
  });

export const entryVersionsQueryOptions = (id: string) =>
  queryOptions({
    queryKey: entryKeys.versions(id),
    queryFn: () => listEntryVersions(id),
    enabled: id.length > 0,
  });

export const entryTagsQueryOptions = () =>
  queryOptions({
    queryKey: entryKeys.tags(),
    queryFn: listTags,
  });
