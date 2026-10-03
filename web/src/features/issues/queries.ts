import { queryOptions } from "@tanstack/react-query";
import { getIssueCard, listIssues } from "@/features/issues/api";
import type { IssueListFilter } from "@/features/issues/types";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const issueKeys = {
  all: ["issues"] as const,
  lists: () => [...issueKeys.all, "list"] as const,
  list: (projectId: string, filter: IssueListFilter) =>
    [...issueKeys.lists(), projectId, filter.status, filter.authorProjectId] as const,
  details: () => [...issueKeys.all, "detail"] as const,
  detail: (projectId: string, issueId: string) =>
    [...issueKeys.details(), projectId, issueId] as const,
};

export const issueListQueryOptions = (projectId: string, filter: IssueListFilter) =>
  queryOptions({
    queryKey: issueKeys.list(projectId, filter),
    queryFn: () => listIssues(projectId, filter),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const issueCardQueryOptions = (projectId: string, issueId: string) =>
  queryOptions({
    queryKey: issueKeys.detail(projectId, issueId),
    queryFn: () => getIssueCard(projectId, issueId),
    enabled: projectId.length > 0 && issueId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
