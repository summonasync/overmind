"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { movePlanningFeatureQueue, planningKeys } from "@/features/planning";
import { lanesKeys } from "@/features/lanes/queries";

interface QueueMoveInput {
  slug: string;
  position: number;
}

export interface LaneMoveController {
  busy: boolean;
  error: Error | null;
  move: (slug: string, position: number) => void;
}

// One instance per board: every control addresses an absolute band slot numbered from the data the board
// currently holds, so a second request started before the first one lands is aimed at a numbering that no
// longer exists. Sharing the mutation is what makes the second click a no-op instead of a wrong write.
export function useFeatureQueueMove(projectId: string): LaneMoveController {
  const queryClient = useQueryClient();

  // Deliberately no optimistic reordering: the server removes the feature and re-inserts it on the target
  // slot, then renumbers the whole band, so the features between the pair move too and only the server
  // knows where they land.
  const mutation = useMutation({
    mutationFn: ({ slug, position }: QueueMoveInput) =>
      movePlanningFeatureQueue(projectId, slug, { move: "toPosition", position }),
    // Awaited: React Query dispatches success once onSuccess returns, so a fire-and-forget invalidation
    // would drop busy while the refetch is in flight and re-arm the controls against pre-move targets.
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: planningKeys.board(projectId) }),
        queryClient.invalidateQueries({ queryKey: lanesKeys.project(projectId) }),
      ]);
    },
  });

  return {
    busy: mutation.isPending,
    error: mutation.error,
    move: (slug, position) => mutation.mutate({ slug, position }),
  };
}
