"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cn } from "@/shared/lib/cn";
import { ErrorText } from "@/shared/ui/error-text";
import { Select } from "@/shared/ui/select";
import { updatePlanningFeature } from "@/features/planning/api";
import { planningKeys } from "@/features/planning/queries";
import { FEATURE_PRIORITIES } from "@/features/planning/types";
import type {
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureResponse,
} from "@/features/planning/types";

type PrioritySelectSize = "sm" | "md";

const PRIORITY_TONE_CLASSES: Record<PlanningFeaturePriority, string> = {
  p0: "border-danger/40 text-danger!",
  p1: "border-warning/40 text-warning!",
  p2: "border-success/40 text-success!",
  p3: "border-border/50 text-muted/70!",
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
      <Select
        size={size}
        aria-label={`Feature priority for ${title}`}
        className={cn(PRIORITY_TONE_CLASSES[priority], size === "sm" && "font-medium")}
        value={priority}
        disabled={setPriority.isPending}
        onChange={(event) => setPriority.mutate(event.target.value as PlanningFeaturePriority)}
      >
        {FEATURE_PRIORITIES.map((candidate) => (
          <option key={candidate} value={candidate}>
            {candidate.toUpperCase()}
          </option>
        ))}
      </Select>
      <ErrorText error={setPriority.error} />
    </span>
  );
}
