import { queryOptions } from "@tanstack/react-query";
import { getDrift, listKits } from "@/features/kits/api";

export const kitKeys = {
  all: ["kits"] as const,
  lists: () => [...kitKeys.all, "list"] as const,
  detail: (id: string) => [...kitKeys.all, "detail", id] as const,
  drifts: () => [...kitKeys.all, "drift"] as const,
  drift: (projectId: string) => [...kitKeys.drifts(), projectId] as const,
};

export const kitListQueryOptions = () =>
  queryOptions({
    queryKey: kitKeys.lists(),
    queryFn: listKits,
  });

export const driftQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: kitKeys.drift(projectId),
    queryFn: () => getDrift(projectId),
    enabled: projectId.length > 0,
  });
