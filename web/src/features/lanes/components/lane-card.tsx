"use client";

import { Pencil, Trash2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { cn } from "@/shared/lib/cn";
import { formatFeatureNumber } from "@/shared/lib/feature-number";
import type { ProjectNumbering } from "@/shared/lib/feature-number";
import { priorityTone } from "@/features/planning";
import type {
  PlanningFeatureBoardItem,
} from "@/features/planning";
import { lanePriorityFromValue } from "@/features/lanes/lib/lane-priority";
import type { LaneMoveTarget } from "@/features/lanes/lib/lane-order";
import type { LaneDropBlockReason } from "@/features/lanes/lib/lane-drop-resolution";
import type { LaneDropError } from "@/features/lanes/lib/use-lane-drop";
import { LaneClaimChip } from "@/features/lanes/components/lane-claim-chip";
import { LaneFeatureMove } from "@/features/lanes/components/lane-feature-move";
import { LaneFeatureWidget } from "@/features/lanes/components/lane-feature-widget";
import { FeatureLaneActions } from "@/features/lanes/components/feature-lane-actions";
import { LaneDropNote } from "@/features/lanes/components/lane-drop-note";
import { LaneSortableCard } from "@/features/lanes/components/lane-sortable-card";
import { LaneSortableList } from "@/features/lanes/components/lane-sortable-list";
import type { LaneMoveController } from "@/features/lanes/lib/use-feature-queue-move";
import type { LaneFeatureSummary, LaneRow } from "@/features/lanes/types";

interface LaneCardProps {
  lane: LaneRow;
  projectId: string;
  numbering: ProjectNumbering;
  boardItems: ReadonlyMap<string, PlanningFeatureBoardItem>;
  features: LaneFeatureSummary[];
  moveTargets: ReadonlyMap<string, LaneMoveTarget>;
  queueMove: LaneMoveController;
  blockReason: LaneDropBlockReason | null;
  draggedSlug: string | null;
  dropError: LaneDropError | null;
  onEdit: (lane: LaneRow) => void;
  onDelete: (lane: LaneRow) => void;
}

interface LaneFeatureRowProps {
  projectId: string;
  numbering: ProjectNumbering;
  feature: LaneFeatureSummary;
  laneName: string;
  boardItem: PlanningFeatureBoardItem | undefined;
  target: LaneMoveTarget | undefined;
  queueMove: LaneMoveController;
  sortableDisabled: boolean;
  dropMessage: string | null;
}

function blockChipLabel(lane: LaneRow, reason: LaneDropBlockReason): string {
  return reason === "claim"
    ? `held by ${lane.holder?.alias ?? "another session"}`
    : "serial — one open feature at a time";
}

function LaneFeatureRow({
  projectId,
  numbering,
  feature,
  laneName,
  boardItem,
  target,
  queueMove,
  sortableDisabled,
  dropMessage,
}: LaneFeatureRowProps) {
  const reorderable = boardItem !== undefined && target !== undefined;

  return (
    <li className="flex flex-col gap-1">
      <LaneSortableCard slug={feature.slug} laneName={laneName} disabled={sortableDisabled}>
        {({ isDragging, isOver }) => (
          <div
            className={cn(
              "rounded-lg transition-colors",
              isDragging && "opacity-40 ring-1 ring-primary/50",
              isOver && !isDragging && "bg-primary/10",
            )}
          >
            <LaneFeatureWidget
              slug={feature.slug}
              number={formatFeatureNumber(feature.slug, numbering.style, numbering.prefix)}
              title={boardItem?.title ?? null}
              status={feature.status}
              priority={boardItem?.priority}
              blocked={boardItem?.isBlocked ?? false}
              taskDone={boardItem?.taskDone}
              taskTotal={boardItem?.taskTotal}
              claim={
                <LaneClaimChip
                  alias={feature.claimedByAlias}
                  vendor={feature.claimedByVendor}
                  claimState={feature.claimState}
                />
              }
            >
              {reorderable ? (
                <LaneFeatureMove
                  slug={feature.slug}
                  laneName={laneName}
                  priority={boardItem.priority}
                  target={target}
                  queueMove={queueMove}
                />
              ) : null}
              <FeatureLaneActions projectId={projectId} slug={feature.slug} currentLane={laneName} />
            </LaneFeatureWidget>
          </div>
        )}
      </LaneSortableCard>
      {dropMessage !== null ? <LaneDropNote message={dropMessage} /> : null}
    </li>
  );
}

export function LaneCard({
  lane,
  projectId,
  numbering,
  boardItems,
  features,
  moveTargets,
  queueMove,
  blockReason,
  draggedSlug,
  dropError,
  onEdit,
  onDelete,
}: LaneCardProps) {
  const lanePriority = lane.priority === null ? undefined : lanePriorityFromValue(lane.priority);
  const slugs = features.map((feature) => feature.slug);

  return (
    <li className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 transition-colors hover:border-border-strong">
      <div className="flex items-start justify-between gap-2">
        <div className="flex min-w-0 items-center gap-1.5">
          <p className="min-w-0 truncate text-sm font-semibold text-foreground">{lane.name}</p>
          {lanePriority !== undefined ? (
            <Badge tone={priorityTone(lanePriority)}>{lanePriority}</Badge>
          ) : null}
        </div>
        <div className="flex shrink-0 items-center gap-1.5">
          <Badge tone={lane.openFeatureCount > 0 ? "warning" : "neutral"}>
            {lane.openFeatureCount} open
          </Badge>
          <button
            type="button"
            aria-label={`Edit lane ${lane.name}`}
            onClick={() => onEdit(lane)}
            className="rounded p-1 text-muted transition-colors hover:bg-primary/10 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            <Pencil size={13} aria-hidden />
          </button>
          <button
            type="button"
            aria-label={`Delete lane ${lane.name}`}
            onClick={() => onDelete(lane)}
            className="rounded p-1 text-muted transition-colors hover:bg-danger/10 hover:text-danger focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            <Trash2 size={13} aria-hidden />
          </button>
        </div>
      </div>

      {lane.contour !== null ? (
        <Badge className="max-w-full self-start whitespace-normal break-all">{lane.contour}</Badge>
      ) : null}

      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-xs text-muted">holder</span>
        {lane.holder === null ? (
          <span className="text-xs text-muted/70">unclaimed</span>
        ) : (
          <LaneClaimChip
            alias={lane.holder.alias}
            vendor={lane.holder.vendor}
            claimState={lane.holder.claimState}
          />
        )}
        {blockReason !== null ? (
          <Badge tone="warning" className="whitespace-normal">
            {blockChipLabel(lane, blockReason)}
          </Badge>
        ) : null}
      </div>

      <LaneSortableList
        laneName={lane.name}
        slugs={slugs}
        disabled={blockReason !== null}
        className={cn(
          "space-y-1.5 rounded-md border-t border-border pt-2 transition-opacity",
          blockReason !== null && "opacity-50",
        )}
        dropClassName="bg-primary/5"
      >
        {({ isDropActive }) =>
          features.length === 0 ? (
            <li
              className={cn(
                "rounded-md border border-dashed border-border px-2 py-3 text-center text-xs text-muted",
                isDropActive && "border-primary text-primary",
              )}
            >
              Drop a feature here to assign it to {lane.name}
            </li>
          ) : (
            features.map((feature) => (
              <LaneFeatureRow
                key={feature.slug}
                projectId={projectId}
                numbering={numbering}
                feature={feature}
                laneName={lane.name}
                boardItem={boardItems.get(feature.slug)}
                target={moveTargets.get(feature.slug)}
                queueMove={queueMove}
                sortableDisabled={blockReason !== null && feature.slug !== draggedSlug}
                dropMessage={dropError?.slug === feature.slug ? dropError.message : null}
              />
            ))
          )
        }
      </LaneSortableList>
    </li>
  );
}
