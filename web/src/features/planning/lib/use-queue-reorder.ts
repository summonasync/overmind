"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { reorderPlanningQueue } from "@/features/planning/api";
import { queueBandSlugs } from "@/features/planning/lib/board-order";
import { planningKeys } from "@/features/planning/queries";
import type {
  PlanningFeatureBoardItem,
  ReorderPlanningQueueValues,
} from "@/features/planning/types";

export function useQueueReorder(projectId: string) {
  const queryClient = useQueryClient();
  const boardKey = planningKeys.board(projectId);

  const reorderQueue = useMutation({
    mutationFn: (values: ReorderPlanningQueueValues) => reorderPlanningQueue(projectId, values),
    onMutate: async (values) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const snapshot = queryClient.getQueryData<PlanningFeatureBoardItem[]>(boardKey);
      queryClient.setQueryData<PlanningFeatureBoardItem[]>(boardKey, (board) => {
        if (board === undefined) {
          return board;
        }
        const positions = new Map(values.slugs.map((slug, index) => [slug, index + 1]));
        return board.map((item) => {
          const queuePosition = positions.get(item.slug);
          return queuePosition === undefined ? item : { ...item, queuePosition };
        });
      });
      return { snapshot };
    },
    onError: (_error, _values, context) => {
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(boardKey, context.snapshot);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: boardKey });
    },
  });

  const reorderCard = (dragSlug: string, targetSlug: string) => {
    const board = queryClient.getQueryData<PlanningFeatureBoardItem[]>(boardKey);
    const dragged = board?.find((item) => item.slug === dragSlug);
    if (board === undefined || dragged === undefined) {
      return;
    }
    const slugs = queueBandSlugs(board, dragged.priority, dragSlug, targetSlug);
    if (slugs !== null) {
      reorderQueue.mutate({ priority: dragged.priority, slugs });
    }
  };

  return { reorderQueue, reorderCard };
}
