import { queryOptions } from "@tanstack/react-query";
import { getClaimSettings, getProject, listProjects } from "@/features/projects/api";
import { getTreeRoots, listDirectory } from "@/shared/api/system";

export const projectKeys = {
  all: ["projects"] as const,
  lists: () => [...projectKeys.all, "list"] as const,
  detail: (id: string) => [...projectKeys.all, "detail", id] as const,
  tree: (path: string) => [...projectKeys.all, "tree", path] as const,
  claimSettings: (projectId: string) => [...projectKeys.all, "claim-settings", projectId] as const,
};

export const projectListQueryOptions = () =>
  queryOptions({
    queryKey: projectKeys.lists(),
    queryFn: listProjects,
  });

export const projectDetailQueryOptions = (id: string) =>
  queryOptions({
    queryKey: projectKeys.detail(id),
    queryFn: () => getProject(id),
    enabled: id.length > 0,
  });

export const claimSettingsQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: projectKeys.claimSettings(projectId),
    queryFn: () => getClaimSettings(projectId),
    enabled: projectId.length > 0,
  });

export const treeRootsQueryOptions = () =>
  queryOptions({
    queryKey: projectKeys.tree("roots"),
    queryFn: getTreeRoots,
    staleTime: 60_000,
  });

export const directoryQueryOptions = (path: string | null) =>
  queryOptions({
    queryKey: projectKeys.tree(path ?? "none"),
    queryFn: () => listDirectory(path as string),
    enabled: path !== null,
    staleTime: 60_000,
  });
