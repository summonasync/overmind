"use client";

import { useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Pencil, Trash2, UserPlus, UserX } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { updatePlanningFeature } from "@/features/planning/api";
import { FeatureNumberBadge } from "@/features/planning/components/feature-number-badge";
import { FeaturePrioritySelect } from "@/features/planning/components/feature-priority-select";
import { compareBoardItems } from "@/features/planning/lib/board-order";
import { cardBriefLine, cardPhaseLabel } from "@/features/planning/lib/board-card-context";
import { allowedTransitions, featureStatusOptions } from "@/features/planning/lib/feature-transitions";
import { featureStatusTone } from "@/features/planning/lib/status-tone";
import { selectToneClasses } from "@/features/planning/lib/select-tone-classes";
import { formatRelativeTime } from "@/shared/lib/relative-time";
import { FeatureClaimChip } from "@/features/planning/components/feature-claim-chip";
import { useDragAutoscroll } from "@/features/planning/lib/use-drag-autoscroll";
import { useQueueReorder } from "@/features/planning/lib/use-queue-reorder";
import { planningKeys } from "@/features/planning/queries";
import type {
  PlanningArtifactStatus,
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureStatus,
} from "@/features/planning/types";

interface FeatureBoardProps {
  projectId: string;
  features: PlanningFeatureBoardItem[];
  onCreate: () => void;
  onEdit: (item: PlanningFeatureBoardItem) => void;
  onDelete: (item: PlanningFeatureBoardItem) => void;
  onClaim: (item: PlanningFeatureBoardItem) => void;
  onUnclaim: (item: PlanningFeatureBoardItem) => void;
  claimPending: boolean;
}

function artifactChip(label: string, status: PlanningArtifactStatus | null) {
  if (status === null) {
    return null;
  }
  return (
    <Badge tone={status === "validated" ? "success" : "neutral"}>
      {label} {status}
    </Badge>
  );
}

interface FeatureStatusSelectProps {
  projectId: string;
  item: PlanningFeatureBoardItem;
}

interface CardDrag {
  slug: string;
  priority: PlanningFeaturePriority;
}

function isInteractiveTarget(target: EventTarget | null): boolean {
  return (
    target instanceof HTMLElement &&
    target.closest("a, button, select, input, textarea") !== null
  );
}

function FeatureStatusSelect({ projectId, item }: FeatureStatusSelectProps) {
  const queryClient = useQueryClient();
  const boardKey = planningKeys.board(projectId);

  const setStatus = useMutation({
    mutationFn: (status: PlanningFeatureStatus) =>
      updatePlanningFeature(projectId, item.slug, { status }),
    onMutate: async (status) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const snapshot = queryClient.getQueryData<PlanningFeatureBoardItem[]>(boardKey);
      queryClient.setQueryData<PlanningFeatureBoardItem[]>(boardKey, (board) =>
        board === undefined
          ? board
          : board.map((candidate) =>
              candidate.slug === item.slug ? { ...candidate, status } : candidate,
            ),
      );
      return { snapshot };
    },
    onError: (_error, _status, context) => {
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(boardKey, context.snapshot);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: boardKey });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, item.slug) });
    },
  });

  return (
    <span className="flex flex-col items-end gap-1">
      <select
        aria-label={`Feature status for ${item.title}`}
        className={cn(
          "rounded-md border bg-surface px-1.5 py-0.5 text-xs font-medium",
          "hover:border-border-strong focus-visible:border-border-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          selectToneClasses[featureStatusTone(item.status)],
        )}
        value={item.status}
        disabled={setStatus.isPending || allowedTransitions(item.status).length === 0}
        onChange={(event) => setStatus.mutate(event.target.value as PlanningFeatureStatus)}
      >
        {featureStatusOptions(item.status).map((status) => (
          <option key={status} value={status}>
            {status}
          </option>
        ))}
      </select>
      <ErrorText error={setStatus.error} />
    </span>
  );
}

export function FeatureBoard({
  projectId,
  features,
  onCreate,
  onEdit,
  onDelete,
  onClaim,
  onUnclaim,
  claimPending,
}: FeatureBoardProps) {
  const [drag, setDrag] = useState<CardDrag | null>(null);
  const [dropTargetSlug, setDropTargetSlug] = useState<string | null>(null);
  const { reorderQueue, reorderCard } = useQueueReorder(projectId);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const { onDragOver: autoscrollDragOver, onDragLeave: autoscrollDragLeave } = useDragAutoscroll({
    active: drag !== null,
    containerRef,
  });

  if (features.length === 0) {
    return (
      <EmptyState
        title="No features yet"
        hint="Create the first feature to plan it as spec, tasks and result."
        action={<Button onClick={onCreate}>New feature</Button>}
      />
    );
  }

  const sortedFeatures = [...features].sort(compareBoardItems);
  const clearDrag = () => {
    setDrag(null);
    setDropTargetSlug(null);
  };

  return (
    <div
      ref={containerRef}
      className="space-y-3"
      onDragOver={autoscrollDragOver}
      onDragLeave={autoscrollDragLeave}
    >
      <ErrorText error={reorderQueue.error} />
      <ul className="grid gap-3 md:grid-cols-2 2xl:grid-cols-3">
        {sortedFeatures.map((item) => {
          const taskPercent = item.taskTotal > 0 ? Math.round((item.taskDone / item.taskTotal) * 100) : 0;
          const draggable = item.status !== "done" && !reorderQueue.isPending;
          const isDropTarget = drag !== null && dropTargetSlug === item.slug;
          const brief = cardBriefLine(item);
          return (
            <li
              key={item.slug}
              draggable={draggable}
              onDragStart={(event) => {
                if (isInteractiveTarget(event.target)) {
                  event.preventDefault();
                  return;
                }
                setDrag({ slug: item.slug, priority: item.priority });
                event.dataTransfer.effectAllowed = "move";
                event.dataTransfer.setData("text/plain", item.slug);
              }}
              onDragOver={(event) => {
                if (
                  drag === null ||
                  drag.slug === item.slug ||
                  drag.priority !== item.priority ||
                  item.status === "done"
                ) {
                  return;
                }
                event.preventDefault();
                event.dataTransfer.dropEffect = "move";
                setDropTargetSlug(item.slug);
              }}
              onDrop={(event) => {
                event.preventDefault();
                if (drag !== null && dropTargetSlug !== null && dropTargetSlug !== drag.slug) {
                  reorderCard(drag.slug, dropTargetSlug);
                }
                clearDrag();
              }}
              onDragEnd={clearDrag}
              className={cn(
                "flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 transition-colors hover:border-border-strong",
                draggable && "cursor-grab",
                drag?.slug === item.slug && "opacity-50",
                isDropTarget && "ring-2 ring-primary/40",
              )}
            >
              <div className="flex items-start justify-between gap-2">
                <a
                  href={`/dashboard?project=${projectId}&feature=${item.slug}`}
                  className="group min-w-0 rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                >
                  <span className="flex items-center gap-1.5">
                    <FeatureNumberBadge slug={item.slug} />
                    <span className="min-w-0 flex-1 truncate text-sm font-semibold text-foreground group-hover:text-primary">
                      {item.title}
                    </span>
                  </span>
                  <span className="block truncate font-mono text-xs text-muted">{item.slug}</span>
                </a>
                <div className="flex shrink-0 items-center gap-1.5">
                  {item.isBlocked ? <Badge tone="danger">blocked</Badge> : null}
                  <span
                    className="shrink-0 text-[10px] uppercase tracking-wide text-muted"
                    title={`Ceremony phase: ${cardPhaseLabel(item)}`}
                  >
                    {cardPhaseLabel(item)}
                  </span>
                  <FeatureStatusSelect projectId={projectId} item={item} />
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-1.5">
                <FeaturePrioritySelect
                  projectId={projectId}
                  slug={item.slug}
                  title={item.title}
                  priority={item.priority}
                  size="sm"
                />
                <Badge>v{item.version}</Badge>
                {artifactChip("spec", item.specStatus)}
                {artifactChip("plan", item.planStatus)}
                <FeatureClaimChip item={item} />
              </div>

              {brief !== null ? <p className="text-xs text-muted">{brief}</p> : null}

              {item.taskTotal > 0 ? (
                <div className="space-y-1">
                  <div className="flex items-center justify-between text-xs text-muted">
                    <span>
                      {item.taskDone}/{item.taskTotal} done
                    </span>
                    <span>{taskPercent}%</span>
                  </div>
                  <div
                    className="h-1 overflow-hidden rounded-full bg-surface-raised"
                    role="progressbar"
                    aria-valuenow={taskPercent}
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-label={`Tasks done for ${item.title}`}
                  >
                    <div className="h-full rounded-full bg-primary" style={{ width: `${taskPercent}%` }} />
                  </div>
                </div>
              ) : null}

              <div className="mt-auto flex items-center justify-between gap-2 border-t border-border pt-2">
                <span className="truncate text-xs text-muted">
                  updated {formatRelativeTime(item.updatedAt)}
                </span>
                <div className="flex shrink-0 gap-1">
                  <Button
                    variant="ghost"
                    className="px-2 py-1 text-xs"
                    disabled={claimPending}
                    onClick={() => (item.claimedByAlias === null ? onClaim(item) : onUnclaim(item))}
                  >
                    {item.claimedByAlias === null ? (
                      <UserPlus size={12} aria-hidden />
                    ) : (
                      <UserX size={12} aria-hidden />
                    )}
                    {item.claimedByAlias === null ? "Claim" : "Unclaim"}
                  </Button>
                  <Button variant="ghost" className="px-2 py-1 text-xs" onClick={() => onEdit(item)}>
                    <Pencil size={12} aria-hidden />
                    Edit
                  </Button>
                  <Button
                    variant="ghost"
                    className="px-2 py-1 text-xs text-danger hover:bg-danger/10 hover:text-danger"
                    onClick={() => onDelete(item)}
                  >
                    <Trash2 size={12} aria-hidden />
                    Delete
                  </Button>
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
