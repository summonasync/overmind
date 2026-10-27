"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { deleteLane, updateLane } from "@/features/lanes/api";
import { planningKeys } from "@/features/planning";
import { lanesKeys } from "@/features/lanes/queries";
import type { LaneUpsertInput } from "@/features/lanes/types";

export function useLaneMutations(projectId: string) {
  const queryClient = useQueryClient();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: lanesKeys.project(projectId) });
    queryClient.invalidateQueries({ queryKey: planningKeys.board(projectId) });
  };

  const editLane = useMutation({
    mutationFn: (lane: LaneUpsertInput) => updateLane(projectId, lane),
    onSuccess: invalidate,
  });

  const removeLane = useMutation({
    mutationFn: (name: string) => deleteLane(projectId, name),
    onSuccess: invalidate,
  });

  return { editLane, removeLane };
}
