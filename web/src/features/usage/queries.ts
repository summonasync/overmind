import { queryOptions } from "@tanstack/react-query";
import { getUsageSummary } from "@/features/usage/api";

export const usageKeys = {
  all: ["usage"] as const,
  summary: (projectId: string, since?: string, until?: string) =>
    [...usageKeys.all, "summary", projectId, since ?? null, until ?? null] as const,
};

export const usageSummaryQueryOptions = (projectId: string, since?: string, until?: string) =>
  queryOptions({
    queryKey: usageKeys.summary(projectId, since, until),
    queryFn: () => getUsageSummary(projectId, since, until),
    enabled: projectId.length > 0,
  });
