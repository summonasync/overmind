"use client";

import { useCallback, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { movePlanningFeatureQueue, planningKeys, updatePlanningFeature } from "@/features/planning";
import type { PlanningFeatureBoardItem } from "@/features/planning";
import { lanesKeys } from "@/features/lanes/queries";
import { previewBandReorder } from "@/features/lanes/lib/lane-band-preview";
import { previewBoardLane, previewLaneAssignment } from "@/features/lanes/lib/lane-optimistic";
import { LANE_DROP_BUSY_MESSAGE } from "@/features/lanes/lib/lane-drop-plan";
import { UNASSIGNED_LANE } from "@/features/lanes/lib/use-feature-lane";
import type { LaneDropPlan } from "@/features/lanes/lib/lane-drop-plan";
import type { LanesRoster } from "@/features/lanes/types";

export interface LaneDropError {
  slug: string;
  message: string;
}

export interface LaneDropController {
  busy: boolean;
  dropError: LaneDropError | null;
  clearDropError: () => void;
  applyDrop: (plan: LaneDropPlan) => void;
}

interface Snapshot {
  board: PlanningFeatureBoardItem[] | undefined;
  lanes: LanesRoster | undefined;
}

interface AssignInput {
  slug: string;
  lane: string;
}

interface QueueMoveInput {
  slug: string;
  anchorSlug: string;
  position: number;
}

function dropFailureMessage(error: unknown): string {
  const detail =
    error instanceof Error && error.message.length > 0 ? error.message : "the board rejected the move";
  return `Move rolled back — ${detail}`;
}

export function useLaneDrop(projectId: string): LaneDropController {
  const queryClient = useQueryClient();
  const [dropError, setDropError] = useState<LaneDropError | null>(null);
  const boardKey = planningKeys.board(projectId);
  const lanesKey = lanesKeys.project(projectId);

  const takeSnapshot = useCallback(
    (): Snapshot => ({
      board: queryClient.getQueryData<PlanningFeatureBoardItem[]>(boardKey),
      lanes: queryClient.getQueryData<LanesRoster>(lanesKey),
    }),
    [queryClient, boardKey, lanesKey],
  );

  const rollback = useCallback(
    (snapshot: Snapshot | undefined) => {
      if (snapshot === undefined) {
        return;
      }
      queryClient.setQueryData(boardKey, snapshot.board);
      queryClient.setQueryData(lanesKey, snapshot.lanes);
    },
    [queryClient, boardKey, lanesKey],
  );

  const toPreviewLane = (lane: string): string | null => (lane === UNASSIGNED_LANE ? null : lane);

  const assignMutation = useMutation({
    mutationFn: ({ slug, lane }: AssignInput) => updatePlanningFeature(projectId, slug, { lane }),
    onMutate: ({ slug, lane }): Snapshot => {
      const snapshot = takeSnapshot();
      if (snapshot.board !== undefined) {
        queryClient.setQueryData(boardKey, previewBoardLane(snapshot.board, slug, toPreviewLane(lane)));
      }
      if (snapshot.lanes !== undefined) {
        queryClient.setQueryData(lanesKey, previewLaneAssignment(snapshot.lanes, slug, toPreviewLane(lane)));
      }
      return snapshot;
    },
    onSuccess: async (_feature, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: boardKey }),
        queryClient.invalidateQueries({ queryKey: lanesKey }),
        queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, variables.slug) }),
      ]);
    },
    onError: (error, variables, context) => {
      rollback(context);
      setDropError({ slug: variables.slug, message: dropFailureMessage(error) });
    },
  });

  const moveMutation = useMutation({
    mutationFn: ({ slug, position }: QueueMoveInput) =>
      movePlanningFeatureQueue(projectId, slug, { move: "toPosition", position }),
    onMutate: ({ slug, anchorSlug }): Snapshot => {
      const snapshot = takeSnapshot();
      if (snapshot.board !== undefined) {
        const previewed = previewBandReorder(snapshot.board, slug, anchorSlug);
        if (previewed !== null) {
          queryClient.setQueryData(boardKey, previewed);
        }
      }
      return snapshot;
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: boardKey }),
        queryClient.invalidateQueries({ queryKey: lanesKey }),
      ]);
    },
    onError: (error, variables, context) => {
      rollback(context);
      setDropError({ slug: variables.slug, message: dropFailureMessage(error) });
    },
  });

  const applyDrop = useCallback(
    (plan: LaneDropPlan) => {
      if (assignMutation.isPending || moveMutation.isPending) {
        if (plan.kind !== "skip") {
          setDropError({ slug: plan.slug, message: LANE_DROP_BUSY_MESSAGE });
        }
        return;
      }
      switch (plan.kind) {
        case "skip":
          break;
        case "refuse":
          setDropError({ slug: plan.slug, message: plan.message });
          break;
        case "assign":
          setDropError(null);
          assignMutation.mutate({ slug: plan.slug, lane: plan.lane });
          break;
        case "unassign":
          setDropError(null);
          assignMutation.mutate({ slug: plan.slug, lane: UNASSIGNED_LANE });
          break;
        case "queue-move":
          setDropError(null);
          moveMutation.mutate({ slug: plan.slug, anchorSlug: plan.anchorSlug, position: plan.position });
          break;
      }
    },
    [assignMutation, moveMutation],
  );

  return {
    busy: assignMutation.isPending || moveMutation.isPending,
    dropError,
    clearDropError: useCallback(() => setDropError(null), []),
    applyDrop,
  };
}
