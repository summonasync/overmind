"use client";

import { useMemo } from "react";
import { Pencil, Trash2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { featureStatusTone, priorityTone } from "@/features/planning";
import type {
  PlanningFeatureBoardItem,
  PlanningFeatureStatus,
} from "@/features/planning";
import { isStatusVisible } from "@/features/lanes/lib/lane-filters";
import { lanePriorityFromValue } from "@/features/lanes/lib/lane-priority";
import { sortLaneFeatures } from "@/features/lanes/lib/lane-order";
import type { LaneMoveTarget } from "@/features/lanes/lib/lane-order";
import { LaneClaimChip } from "@/features/lanes/components/lane-claim-chip";
import { LaneFeatureMove } from "@/features/lanes/components/lane-feature-move";
import { FeatureLaneActions } from "@/features/lanes/components/feature-lane-actions";
import type { LaneMoveController } from "@/features/lanes/lib/use-feature-queue-move";
import type { LaneFeatureSummary, LaneRow } from "@/features/lanes/types";

interface LaneCardProps {
  lane: LaneRow;
  projectId: string;
  boardItems: ReadonlyMap<string, PlanningFeatureBoardItem>;
  moveTargets: ReadonlyMap<string, LaneMoveTarget>;
  queueMove: LaneMoveController;
  statusFilter: ReadonlySet<PlanningFeatureStatus>;
  onEdit: (lane: LaneRow) => void;
  onDelete: (lane: LaneRow) => void;
}

interface LaneFeatureRowProps {
  projectId: string;
  feature: LaneFeatureSummary;
  laneName: string;
  boardItem: PlanningFeatureBoardItem | undefined;
  target: LaneMoveTarget | undefined;
  queueMove: LaneMoveController;
}

function LaneFeatureRow({
  projectId,
  feature,
  laneName,
  boardItem,
  target,
  queueMove,
}: LaneFeatureRowProps) {
  const priority = boardItem?.priority;
  const reorderable = boardItem !== undefined && target !== undefined;
  const actions = <FeatureLaneActions projectId={projectId} slug={feature.slug} currentLane={laneName} />;

  return (
    <li className="flex flex-col gap-1">
      <div className="flex items-center gap-2">
        <span className="min-w-0 truncate font-mono text-xs text-foreground">{feature.slug}</span>
        {priority !== undefined ? <Badge tone={priorityTone(priority)}>{priority}</Badge> : null}
        <Badge tone={featureStatusTone(feature.status)}>{feature.status}</Badge>
        <span className="ml-auto min-w-0 truncate">
          <LaneClaimChip
            alias={feature.claimedByAlias}
            vendor={feature.claimedByVendor}
            claimState={feature.claimState}
          />
        </span>
      </div>
      {reorderable ? (
        <div className="flex min-w-0 flex-wrap items-center gap-1">
          <LaneFeatureMove
            slug={feature.slug}
            laneName={laneName}
            priority={boardItem.priority}
            target={target}
            queueMove={queueMove}
          />
          {actions}
        </div>
      ) : (
        actions
      )}
    </li>
  );
}

export function LaneCard({
  lane,
  projectId,
  boardItems,
  moveTargets,
  queueMove,
  statusFilter,
  onEdit,
  onDelete,
}: LaneCardProps) {
  const visibleFeatures = useMemo(
    () =>
      sortLaneFeatures(
        lane.features.filter((feature) => isStatusVisible(statusFilter, feature.status)),
        boardItems,
      ),
    [lane.features, boardItems, statusFilter],
  );
  const lanePriority = lane.priority === null ? undefined : lanePriorityFromValue(lane.priority);

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
      </div>

      {visibleFeatures.length === 0 ? (
        <p className="text-xs text-muted">No features in this lane yet.</p>
      ) : (
        <ul className="space-y-1.5 border-t border-border pt-2">
          {visibleFeatures.map((feature) => (
            <LaneFeatureRow
              key={feature.slug}
              projectId={projectId}
              feature={feature}
              laneName={lane.name}
              boardItem={boardItems.get(feature.slug)}
              target={moveTargets.get(feature.slug)}
              queueMove={queueMove}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
