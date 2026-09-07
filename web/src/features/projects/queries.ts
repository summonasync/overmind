import { queryOptions } from "@tanstack/react-query";
import { getProject, listProjects } from "@/features/projects/api";

export const projectKeys = {
  all: ["projects"] as const,
  lists: () => [...projectKeys.all, "list"] as const,
  detail: (id: string) => [...projectKeys.all, "detail", id] as const,
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
