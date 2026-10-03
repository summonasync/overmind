"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { appendToIssue, promoteIssue, setIssueStatus } from "@/features/issues/api";
import { issueCardQueryOptions, issueKeys, issueListQueryOptions } from "@/features/issues/queries";
import type { IssueListFilter, PromoteIssueValues } from "@/features/issues/types";

export function useIssueList(projectId: string, filter: IssueListFilter) {
  const query = useQuery(issueListQueryOptions(projectId, filter));
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function useIssueCard(projectId: string, issueId: string) {
  const query = useQuery(issueCardQueryOptions(projectId, issueId));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}

export function useIssueMutations(projectId: string) {
  const queryClient = useQueryClient();

  const invalidate = (issueId: string) => {
    queryClient.invalidateQueries({ queryKey: issueKeys.lists() });
    queryClient.invalidateQueries({ queryKey: issueKeys.detail(projectId, issueId) });
  };

  const triage = useMutation({
    mutationFn: (issueId: string) => setIssueStatus(projectId, issueId, "triaged"),
    onSuccess: (_result, issueId) => invalidate(issueId),
  });

  const close = useMutation({
    mutationFn: (input: { issueId: string; reason: string }) => {
      const reason = input.reason.trim();
      if (reason.length === 0) {
        return setIssueStatus(projectId, input.issueId, "closed");
      }
      return appendToIssue(projectId, input.issueId, {
        authorProjectId: projectId,
        text: reason,
      }).then(() => setIssueStatus(projectId, input.issueId, "closed"));
    },
    onSuccess: (_result, input) => invalidate(input.issueId),
  });

  const promote = useMutation({
    mutationFn: (input: { issueId: string; values: PromoteIssueValues }) =>
      promoteIssue(projectId, input.issueId, input.values),
    onSuccess: (_result, input) => invalidate(input.issueId),
  });

  return { triage, close, promote };
}
