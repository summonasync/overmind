"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { updateClaimSettings } from "@/features/projects/api";
import { claimSettingsQueryOptions, projectKeys } from "@/features/projects/queries";
import type { ClaimSettingsResponse } from "@/features/projects/types";

export function useClaimSettings(projectId: string) {
  const query = useQuery(claimSettingsQueryOptions(projectId));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}

export function useClaimSettingsMutation(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (values: ClaimSettingsResponse) => updateClaimSettings(projectId, values),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: projectKeys.claimSettings(projectId) }),
  });
}
