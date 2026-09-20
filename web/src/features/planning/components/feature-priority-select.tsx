"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cn } from "@/shared/lib/cn";
import { ErrorText } from "@/shared/ui/error-text";
import { updatePlanningFeature } from "@/features/planning/api";
import { priorityTone } from "@/features/planning/lib/priority-tone";
import { selectToneClasses } from "@/features/planning/lib/select-tone-classes";
import { planningKeys } from "@/features/planning/queries";
import { FEATURE_PRIORITIES } from "@/features/planning/types";
import type {
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureResponse,
} from "@/features/planning/types";

type PrioritySelectSize = "sm" | "md";

const SIZE_CLASSES: Record<PrioritySelectSize, string> = {
  sm: "px-1.5 py-0.5 text-xs font-medium",
  md: "px-2.5 py-1.5 text-sm",
};

interface FeaturePrioritySelectProps {
  projectId: string;
  slug: string;
  title: string;
  priority: PlanningFeaturePriority;
  size?: PrioritySelectSize;
}

export function FeaturePrioritySelect({
  projectId,
  slug,
  title,
  priority,
  size = "md",
}: FeaturePrioritySelectProps) {
  const queryClient = useQueryClient();
  const boardKey = planningKeys.board(projectId);
  const featureKey = planningKeys.feature(projectId, slug);

  const setPriority = useMutation({
    mutationFn: (next: PlanningFeaturePriority) =>
      updatePlanningFeature(projectId, slug, { priority: next }),
    onMutate: async (next) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      await queryClient.cancelQueries({ queryKey: featureKey });
      const boardSnapshot = queryClient.getQueryData<PlanningFeatureBoardItem[]>(boardKey);
      const featureSnapshot = queryClient.getQueryData<PlanningFeatureResponse>(featureKey);
      queryClient.setQueryData<PlanningFeatureBoardItem[]>(boardKey, (board) =>
        board === undefined
          ? board
          : board.map((candidate) =>
              candidate.slug === slug ? { ...candidate, priority: next } : candidate,
            ),
      );
      queryClient.setQueryData<PlanningFeatureResponse>(featureKey, (current) =>
        current === undefined ? current : { ...current, priority: next },
      );
      return { boardSnapshot, featureSnapshot };
    },
    onError: (_error, _priority, context) => {
      if (context?.boardSnapshot !== undefined) {
        queryClient.setQueryData(boardKey, context.boardSnapshot);
      }
      if (context?.featureSnapshot !== undefined) {
        queryClient.setQueryData(featureKey, context.featureSnapshot);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: boardKey });
      queryClient.invalidateQueries({ queryKey: featureKey });
    },
  });

  return (
    <span className="flex flex-col items-start gap-1">
      <select
        aria-label={`Feature priority for ${title}`}
        className={cn(
          "rounded-md border bg-surface",
          "hover:border-border-strong focus-visible:border-border-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          SIZE_CLASSES[size],
          selectToneClasses[priorityTone(priority)],
        )}
        value={priority}
        disabled={setPriority.isPending}
        onChange={(event) => setPriority.mutate(event.target.value as PlanningFeaturePriority)}
      >
        {FEATURE_PRIORITIES.map((candidate) => (
          <option key={candidate} value={candidate}>
            {candidate.toUpperCase()}
          </option>
        ))}
      </select>
      <ErrorText error={setPriority.error} />
    </span>
  );
}
