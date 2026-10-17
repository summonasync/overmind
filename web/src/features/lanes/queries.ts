import { queryOptions } from "@tanstack/react-query";
import { listLanes } from "@/features/lanes/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const lanesKeys = {
  all: ["lanes"] as const,
  project: (projectId: string) => [...lanesKeys.all, projectId] as const,
};

export const lanesQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: lanesKeys.project(projectId),
    queryFn: () => listLanes(projectId),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
