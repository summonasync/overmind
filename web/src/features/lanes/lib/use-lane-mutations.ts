"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { setLanes } from "@/features/lanes/api";
import { appendLane, removeLane } from "@/features/lanes/lib/lane-roster";
import { lanesKeys } from "@/features/lanes/queries";
import type { LaneRow } from "@/features/lanes/types";

interface CreateLaneInput {
  name: string;
  contour: string | null;
  priority: number | null;
  roster: LaneRow[];
}

interface DeleteLaneInput {
  name: string;
  roster: LaneRow[];
}

export function useLaneMutations(projectId: string) {
  const queryClient = useQueryClient();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: lanesKeys.project(projectId) });
  };

  const createLane = useMutation({
    mutationFn: (input: CreateLaneInput) =>
      setLanes(projectId, appendLane(input.roster, input.name, input.contour, input.priority)),
    onSuccess: invalidate,
  });

  const deleteLane = useMutation({
    mutationFn: (input: DeleteLaneInput) => setLanes(projectId, removeLane(input.roster, input.name)),
    onSuccess: invalidate,
  });

  return { createLane, deleteLane };
}
