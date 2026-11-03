"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { updateApprovalSettings } from "@/features/projects/api";
import { approvalSettingsQueryOptions, projectKeys } from "@/features/projects/queries";
import type {
  ApprovalSettingsResponse,
  DelegateApprovalsOverride,
} from "@/features/projects/types";

export function useApprovalSettings(projectId: string) {
  const query = useQuery(approvalSettingsQueryOptions(projectId));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}

export function useApprovalSettingsMutation(projectId: string) {
  const queryClient = useQueryClient();
  const settingsKey = projectKeys.approvalSettings(projectId);

  return useMutation({
    mutationFn: (delegateApprovals: DelegateApprovalsOverride) =>
      updateApprovalSettings(projectId, delegateApprovals),
    onMutate: async (delegateApprovals) => {
      await queryClient.cancelQueries({ queryKey: settingsKey });
      const snapshot = queryClient.getQueryData<ApprovalSettingsResponse>(settingsKey);
      queryClient.setQueryData<ApprovalSettingsResponse>(settingsKey, {
        delegateApprovals,
        effectiveDelegateApprovals: optimisticEffective(delegateApprovals, snapshot),
      });
      return { snapshot };
    },
    onError: (_error, _delegateApprovals, context) => {
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(settingsKey, context.snapshot);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: settingsKey });
    },
  });
}

function optimisticEffective(
  delegateApprovals: DelegateApprovalsOverride,
  snapshot: ApprovalSettingsResponse | undefined,
): boolean {
  if (delegateApprovals === "on") {
    return true;
  }
  if (delegateApprovals === "off") {
    return false;
  }
  return snapshot?.effectiveDelegateApprovals ?? false;
}
