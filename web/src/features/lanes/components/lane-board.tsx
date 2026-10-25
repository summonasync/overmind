"use client";

import { useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Plus } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { cn } from "@/shared/lib/cn";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import {
  FeatureClaimChip,
  usePlanningBoard,
} from "@/features/planning";
import type { PlanningFeatureBoardItem, PlanningFeatureStatus } from "@/features/planning";
import { formatFeatureNumber } from "@/shared/lib/feature-number";
import type { ProjectNumbering } from "@/shared/lib/feature-number";
import { useProjectNumbering } from "@/features/projects";
import { LaneCard } from "@/features/lanes/components/lane-card";
import { LaneAddDialog } from "@/features/lanes/components/lane-add-dialog";
import { LaneDeleteDialog } from "@/features/lanes/components/lane-delete-dialog";
import { LaneEditDialog } from "@/features/lanes/components/lane-edit-dialog";
import { LaneFeatureWidget } from "@/features/lanes/components/lane-feature-widget";
import { featureHref } from "@/features/lanes/lib/lane-feature-href";
import { FeatureLaneActions } from "@/features/lanes/components/feature-lane-actions";
import { LaneDropNote } from "@/features/lanes/components/lane-drop-note";
import { LaneDndProvider } from "@/features/lanes/components/lane-dnd-context";
import { LaneDraggableCard } from "@/features/lanes/components/lane-draggable-card";
import { LanePoolDroppable } from "@/features/lanes/components/lane-pool-droppable";
import { LaneStatusChips } from "@/features/lanes/components/lane-status-chips";
import { isStatusVisible, toggleStatus } from "@/features/lanes/lib/lane-filters";
import {
  boardItemsBySlug,
  laneMoveTargetsBySlug,
  sortBoardItems,
  sortLaneFeatures,
} from "@/features/lanes/lib/lane-order";
import { laneDropBlockReason, planLaneDrop } from "@/features/lanes/lib/lane-drop-plan";
import type { LaneDropContext } from "@/features/lanes/lib/lane-drop-plan";
import { resolveLaneDrop } from "@/features/lanes/lib/lane-drop-resolution";
import {
  LANE_DND_LANE,
  parseLaneFeatureDragId,
} from "@/features/lanes/lib/lane-dnd-data";
import type {
  LaneDropTargetData,
  LaneFeatureDragData,
} from "@/features/lanes/lib/lane-dnd-data";
import { useLaneDrop } from "@/features/lanes/lib/use-lane-drop";
import { useFeatureQueueMove } from "@/features/lanes/lib/use-feature-queue-move";
import { useLanes } from "@/features/lanes/lib/use-lanes";
import type { LaneRow } from "@/features/lanes/types";

const LANES_DESCRIPTION =
  "Wave view of the planning board: one card per lane with its holder, open-feature count and the features inside. Lanes are managed here — each card edits or deletes its own lane, and a deleted lane leaves its features in the without-lane pool. Cards drag between lanes and the pool; the arrow keys drive the same moves.";

const EMPTY_DROP_CONTEXT: LaneDropContext = {
  slice: [],
  positionBySlug: new Map(),
  priorityBySlug: new Map(),
};

interface UnlanedFeatureRowProps {
  projectId: string;
  numbering: ProjectNumbering;
  item: PlanningFeatureBoardItem;
  dropMessage: string | null;
}

function UnlanedFeatureRow({ projectId, numbering, item, dropMessage }: UnlanedFeatureRowProps) {
  return (
    <li className="flex flex-col gap-1">
      <LaneDraggableCard slug={item.slug} fromLane={null}>
        {({ isDragging }) => (
          <div
            className={cn(
              "rounded-lg transition-opacity",
              isDragging && "opacity-40 ring-1 ring-primary/50",
            )}
          >
            <LaneFeatureWidget
              slug={item.slug}
              number={formatFeatureNumber(item.slug, numbering.style, numbering.prefix)}
              title={item.title}
              status={item.status}
              blocked={item.isBlocked}
              taskDone={item.taskDone}
              taskTotal={item.taskTotal}
              href={featureHref(projectId, item.slug)}
              claimAlias={item.claimedByAlias}
              claimState={item.claimState}
              claim={<FeatureClaimChip item={item} />}
            >
              <FeatureLaneActions projectId={projectId} slug={item.slug} currentLane={null} />
            </LaneFeatureWidget>
          </div>
        )}
      </LaneDraggableCard>
      {dropMessage !== null ? <LaneDropNote message={dropMessage} /> : null}
    </li>
  );
}

export function LaneBoard() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";
  const numbering = useProjectNumbering(projectId);
  const [statusFilter, setStatusFilter] = useState<ReadonlySet<PlanningFeatureStatus>>(new Set());
  const [deletingLane, setDeletingLane] = useState<LaneRow | null>(null);
  const [editingLane, setEditingLane] = useState<LaneRow | null>(null);
  const [adding, setAdding] = useState(false);
  const [dragged, setDragged] = useState<LaneFeatureDragData | null>(null);

  const { data: roster, loading, error } = useLanes(projectId);
  const { data: features, loading: boardLoading, error: boardError } = usePlanningBoard(projectId);
  // The mutation lives here, not on the card: a second move must not start while one is in flight, because
  // its target was numbered against the pre-move band and would land on a slot that no longer means that.
  const queueMove = useFeatureQueueMove(projectId);
  const drop = useLaneDrop(projectId);

  const lanes = useMemo(
    () =>
      (roster?.lanes ?? []).map((lane) => ({
        ...lane,
        features: lane.features.filter((feature) => feature.status !== "done"),
      })),
    [roster],
  );
  const boardItems = useMemo(() => boardItemsBySlug(features), [features]);
  const moveTargets = useMemo(
    () => laneMoveTargetsBySlug(lanes, features, statusFilter),
    [lanes, features, statusFilter],
  );
  const visibleByLane = useMemo(
    () =>
      new Map(
        lanes.map((lane) => {
          const ordered = sortLaneFeatures(
            lane.features.filter((feature) => isStatusVisible(statusFilter, feature.status)),
            boardItems,
          );
          return [lane.name, { features: ordered, slugs: ordered.map((feature) => feature.slug) }] as const;
        }),
      ),
    [lanes, boardItems, statusFilter],
  );
  const dropContexts = useMemo(() => {
    const priorityBySlug = new Map(features.map((item) => [item.slug, item.priority] as const));
    const positionBySlug = new Map(
      [...moveTargets].map(([slug, target]) => [slug, target.position] as const),
    );
    return new Map(
      [...visibleByLane].map(([name, slice]) => [
        name,
        { slice: slice.slugs, positionBySlug, priorityBySlug } as LaneDropContext,
      ]),
    );
  }, [features, moveTargets, visibleByLane]);
  const unlaned = useMemo(
    () =>
      sortBoardItems(
        features.filter(
          (item) =>
            (item.lane === null || item.lane === undefined) &&
            item.status !== "done" &&
            isStatusVisible(statusFilter, item.status),
        ),
      ),
    [features, statusFilter],
  );

  if (!projectId) {
    return (
      <div className="space-y-4">
        <PageHeader title="Lanes" description={LANES_DESCRIPTION} />
        <p className="text-sm text-muted">
          Select a project first.{" "}
          <a
            href="/"
            className="text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            Go to Projects
          </a>
        </p>
      </div>
    );
  }

  const laneBlockReasonFor = (laneName: string, dragSlug: string) => {
    const lane = (roster?.lanes ?? []).find((candidate) => candidate.name === laneName);
    return lane === undefined ? null : laneDropBlockReason(lane, dragSlug);
  };

  const handleDrop = (
    drag: LaneFeatureDragData,
    target: LaneDropTargetData | null,
    overId: unknown,
  ) => {
    const overSlug = parseLaneFeatureDragId(overId);
    const laneName = target !== null && target.kind === LANE_DND_LANE ? target.laneName : null;
    const slice = laneName === null ? EMPTY_DROP_CONTEXT.slice : (visibleByLane.get(laneName)?.slugs ?? []);
    const overIndex = overSlug === null ? slice.length : slice.indexOf(overSlug);
    const action = resolveLaneDrop({
      drag,
      target,
      toIndex: overIndex === -1 ? slice.length : overIndex,
    });
    const blocked = laneName === null ? null : laneBlockReasonFor(laneName, drag.slug);
    drop.applyDrop(
      planLaneDrop({
        action,
        overSlug,
        context: laneName === null ? EMPTY_DROP_CONTEXT : (dropContexts.get(laneName) ?? EMPTY_DROP_CONTEXT),
        blocked,
        moveInFlight: drop.busy,
      }),
    );
  };

  const handleDragChange = (drag: LaneFeatureDragData | null) => {
    setDragged(drag);
    if (drag !== null) {
      drop.clearDropError();
    }
  };

  const poolDisabled = dragged !== null && dragged.fromLane === null;

  return (
    <LaneDndProvider onDrop={handleDrop} onDragChange={handleDragChange}>
      <div className="space-y-4">
        <PageHeader
          title="Lanes"
          description={LANES_DESCRIPTION}
          actions={
            <Button onClick={() => setAdding(true)} disabled={roster === null}>
              <Plus size={14} aria-hidden />
              Add lane
            </Button>
          }
        />

        <ErrorText error={error ?? boardError ?? queueMove.error} />

        {loading ? (
          <LoadingState label="Loading lanes…" />
        ) : lanes.length === 0 ? (
          <EmptyState
            title="No lanes defined"
            hint="The project roster has no lanes yet. Use Add lane in the header to create the first one."
          />
        ) : (
          <ul className="grid gap-3 md:grid-cols-2 2xl:grid-cols-3">
            {lanes.map((lane) => (
              <LaneCard
                key={lane.name}
                lane={lane}
                projectId={projectId}
                numbering={numbering}
                boardItems={boardItems}
                features={visibleByLane.get(lane.name)?.features ?? []}
                moveTargets={moveTargets}
                queueMove={queueMove}
                blockReason={dragged === null ? null : laneDropBlockReason(lane, dragged.slug)}
                draggedSlug={dragged?.slug ?? null}
                dropError={drop.dropError}
                onEdit={setEditingLane}
                onDelete={setDeletingLane}
              />
            ))}
          </ul>
        )}

        <section className="space-y-2">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-semibold text-foreground">Without lane</h2>
            <Badge>{unlaned.length}</Badge>
          </div>
          {boardLoading ? (
            <LoadingState label="Loading board…" />
          ) : (
            <LanePoolDroppable
              className={cn(
                "rounded-lg border border-dashed border-border p-3 transition-colors",
                unlaned.length === 0 && "text-center",
              )}
              dropClassName="border-primary bg-primary/5"
              disabled={poolDisabled}
            >
              {() =>
                unlaned.length === 0 ? (
                  <p className="text-xs text-muted">Every active feature on the board carries a lane.</p>
                ) : (
                  <ul className="space-y-2">
                    {unlaned.map((item) => (
                      <UnlanedFeatureRow
                        key={item.slug}
                        projectId={projectId}
                        numbering={numbering}
                        item={item}
                        dropMessage={drop.dropError?.slug === item.slug ? drop.dropError.message : null}
                      />
                    ))}
                  </ul>
                )
              }
            </LanePoolDroppable>
          )}
        </section>

        <div className="flex flex-wrap items-center gap-3 border-t border-border pt-3">
          <span className="text-xs font-medium text-muted">Status</span>
          <LaneStatusChips
            selected={statusFilter}
            onToggle={(status) => setStatusFilter(toggleStatus(statusFilter, status))}
            onClear={() => setStatusFilter(new Set())}
          />
        </div>

        {adding && roster !== null ? (
          <LaneAddDialog projectId={projectId} roster={lanes} onClose={() => setAdding(false)} />
        ) : null}

        {deletingLane !== null && roster !== null ? (
          <LaneDeleteDialog
            projectId={projectId}
            lane={deletingLane}
            onClose={() => setDeletingLane(null)}
          />
        ) : null}

        {editingLane !== null && roster !== null ? (
          <LaneEditDialog
            projectId={projectId}
            lane={editingLane}
            onClose={() => setEditingLane(null)}
          />
        ) : null}
      </div>
    </LaneDndProvider>
  );
}
