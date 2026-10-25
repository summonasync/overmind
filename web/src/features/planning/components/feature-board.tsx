"use client";

import { useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Pencil, Trash2, UserPlus, UserX } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { updatePlanningFeature } from "@/features/planning/api";
import { FeatureDoneDialog } from "@/features/planning/components/feature-done-dialog";
import { FeatureNumberBadge } from "@/features/planning/components/feature-number-badge";
import { FeaturePrioritySelect } from "@/features/planning/components/feature-priority-select";
import {
  decideDoneGate,
  doneGateDialogTitle,
  doneGateRefusal,
  type DoneGateDecision,
  type DoneGateScenario,
} from "@/features/planning/lib/done-gate";
import { useApproveAndDone } from "@/features/planning/lib/use-approve-and-done";
import { compareBoardItems } from "@/features/planning/lib/board-order";
import {
  cardBriefLine,
  cardClaimActive,
  cardClaimLine,
  cardPhaseLabel,
} from "@/features/planning/lib/board-card-context";
import { allowedTransitions, featureStatusOptions } from "@/features/planning/lib/feature-transitions";
import { featureStatusTone } from "@/features/planning/lib/status-tone";
import { selectToneClasses } from "@/features/planning/lib/select-tone-classes";
import { taskReadiness } from "@/features/planning/lib/task-readiness";
import type { TaskReadinessSegment } from "@/features/planning/lib/task-readiness";
import { formatCalendarDate, formatRelativeTime } from "@/shared/lib/relative-time";
import { FeatureClaimChip } from "@/features/planning/components/feature-claim-chip";
import { useDragAutoscroll } from "@/features/planning/lib/use-drag-autoscroll";
import { useQueueReorder } from "@/features/planning/lib/use-queue-reorder";
import { featureQueryOptions, planningKeys } from "@/features/planning/queries";
import type {
  PlanningArtifactStatus,
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureStatus,
} from "@/features/planning/types";
import type { ProjectNumbering } from "@/shared/lib/feature-number";

interface FeatureBoardProps {
  projectId: string;
  numbering: ProjectNumbering;
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

const segmentToneClasses: Record<TaskReadinessSegment["tone"], string> = {
  neutral: "bg-muted",
  muted: "bg-muted",
  info: "bg-info",
  warning: "bg-warning",
  success: "bg-success",
  primary: "bg-primary",
  agent: "bg-agent",
  marshal: "bg-marshal",
  danger: "bg-danger",
  skill: "bg-type-skill",
  idea: "bg-type-featureRequest",
  audit: "bg-type-audit",
};

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

interface DoneGateState {
  scenario: DoneGateScenario;
  resultArtifactId: string | null;
  refusal: string | null;
}

function FeatureStatusSelect({ projectId, item }: FeatureStatusSelectProps) {
  const queryClient = useQueryClient();
  const boardKey = planningKeys.board(projectId);
  const [gate, setGate] = useState<DoneGateState | null>(null);
  const approveAndDone = useApproveAndDone(projectId, item.slug);

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
    onError: (error, _status, context) => {
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(boardKey, context.snapshot);
      }
      const refusalText = doneGateRefusal(error);
      if (refusalText !== null) {
        setGate({ scenario: "refused", resultArtifactId: null, refusal: refusalText });
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: boardKey });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, item.slug) });
    },
  });

  const closeGate = () => {
    setGate(null);
    setStatus.reset();
    approveAndDone.reset();
  };

  const requestDone = async () => {
    let decision: DoneGateDecision;
    try {
      const feature = await queryClient.fetchQuery(featureQueryOptions(projectId, item.slug));
      decision = decideDoneGate(feature.artifacts);
    } catch {
      decision = { kind: "ready" };
    }
    if (decision.kind === "ready") {
      setStatus.mutate("done");
      return;
    }
    if (decision.kind === "missing-result") {
      setGate({ scenario: "missing-result", resultArtifactId: null, refusal: null });
      return;
    }
    setGate({
      scenario: "unvalidated-result",
      resultArtifactId: decision.resultArtifactId,
      refusal: null,
    });
  };

  const handleStatusChange = (status: PlanningFeatureStatus) => {
    if (status !== "done") {
      setStatus.mutate(status);
      return;
    }
    void requestDone();
  };

  const approve = () => {
    if (gate === null || gate.resultArtifactId === null) {
      return;
    }
    approveAndDone.mutate(gate.resultArtifactId, { onSuccess: closeGate });
  };

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
        onChange={(event) => handleStatusChange(event.target.value as PlanningFeatureStatus)}
      >
        {featureStatusOptions(item.status).map((status) => (
          <option key={status} value={status}>
            {status}
          </option>
        ))}
      </select>
      <ErrorText error={setStatus.error} />
      <Modal
        title={gate !== null ? doneGateDialogTitle(gate.scenario) : ""}
        open={gate !== null}
        onClose={closeGate}
      >
        {gate !== null ? (
          <FeatureDoneDialog
            scenario={gate.scenario}
            projectId={projectId}
            slug={item.slug}
            title={item.title}
            refusal={gate.refusal}
            pending={approveAndDone.isPending}
            error={approveAndDone.error as Error | null}
            onApprove={approve}
            onCancel={closeGate}
          />
        ) : null}
      </Modal>
    </span>
  );
}

export function FeatureBoard({
  projectId,
  numbering,
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
          const claimLine = cardClaimLine(item);
          const phaseLabel = cardPhaseLabel(item);
          const readiness = taskReadiness(item);
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
                cardClaimActive(item) && "border-primary",
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
                    <FeatureNumberBadge slug={item.slug} numbering={numbering} />
                    <span className="min-w-0 flex-1 truncate text-sm font-semibold text-foreground group-hover:text-primary">
                      {item.title}
                    </span>
                  </span>
                  <span className="flex items-center gap-1.5">
                    <span className="min-w-0 truncate font-mono text-xs text-muted">{item.slug}</span>
                    {phaseLabel !== null ? <Badge className="shrink-0">{phaseLabel}</Badge> : null}
                  </span>
                </a>
                {claimLine !== null ? (
                  <span
                    className={cn(
                      "ml-auto min-w-0 truncate text-xs",
                      cardClaimActive(item) ? "text-success" : "text-muted",
                    )}
                  >
                    {claimLine}
                  </span>
                ) : null}
                <div className="flex shrink-0 items-center gap-1.5">
                  {item.isBlocked ? <Badge tone="danger">blocked</Badge> : null}
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

              {readiness !== null ? (
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
                    aria-valuetext={readiness.ariaValueText}
                  >
                    <div className="flex h-full">
                      {readiness.segments.map((segment) => (
                        <div
                          key={segment.tone}
                          className={cn("h-full", segmentToneClasses[segment.tone])}
                          style={{ width: `${segment.fraction * 100}%` }}
                        />
                      ))}
                    </div>
                  </div>
                  <p className="text-xs text-muted">{readiness.countersLine}</p>
                </div>
              ) : null}

              <div className="mt-auto flex items-center justify-between gap-2 border-t border-border pt-2">
                <span
                  className="truncate text-xs text-muted"
                  title={formatCalendarDate(item.updatedAt)}
                >
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
