"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { planningKeys, updatePlanningFeature } from "@/features/planning";
import { lanesKeys } from "@/features/lanes/queries";

export const UNASSIGNED_LANE = "";

interface LaneAssignmentInput {
  slug: string;
  lane: string;
}

export function useFeatureLaneAssignment(projectId: string) {
  const queryClient = useQueryClient();

  // The human door reads lane:null as "leave it alone"; only the empty string clears the assignment.
  return useMutation({
    mutationFn: ({ slug, lane }: LaneAssignmentInput) =>
      updatePlanningFeature(projectId, slug, { lane }),
    onSuccess: (_feature, variables) => {
      queryClient.invalidateQueries({ queryKey: lanesKeys.project(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.board(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, variables.slug) });
    },
  });
}
