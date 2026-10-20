"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Plus } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import {
  FeatureClaimChip,
  FeatureNumberBadge,
  featureStatusTone,
  usePlanningBoard,
} from "@/features/planning";
import type { PlanningFeatureBoardItem, PlanningFeatureStatus } from "@/features/planning";
import type { ProjectNumbering } from "@/shared/lib/feature-number";
import { useProjectNumbering } from "@/features/projects";
import { LaneCard } from "@/features/lanes/components/lane-card";
import { LaneAddDialog } from "@/features/lanes/components/lane-add-dialog";
import { LaneDeleteDialog } from "@/features/lanes/components/lane-delete-dialog";
import { LaneEditDialog } from "@/features/lanes/components/lane-edit-dialog";
import { FeatureLaneActions } from "@/features/lanes/components/feature-lane-actions";
import { LaneStatusChips } from "@/features/lanes/components/lane-status-chips";
import { isStatusVisible, toggleStatus } from "@/features/lanes/lib/lane-filters";
import { priorityBySlug, sortBoardItems } from "@/features/lanes/lib/lane-order";
import { useLanes } from "@/features/lanes/lib/use-lanes";
import type { LaneRow } from "@/features/lanes/types";

const LANES_DESCRIPTION =
  "Wave view of the planning board: one card per lane with its holder, open-feature count and the features inside. Lanes are managed here — each card edits or deletes its own lane, and a deleted lane leaves its features in the without-lane pool.";

function UnlanedFeatureRow({
  projectId,
  numbering,
  item,
}: {
  projectId: string;
  numbering: ProjectNumbering;
  item: PlanningFeatureBoardItem;
}) {
  return (
    <li className="flex flex-col gap-1 rounded-md border border-border bg-surface px-3 py-2">
      <div className="flex flex-wrap items-center gap-2">
        <a
          href={`/dashboard?project=${projectId}&feature=${item.slug}`}
          className="group flex min-w-0 items-center gap-2 rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <FeatureNumberBadge slug={item.slug} numbering={numbering} />
          <span className="min-w-0 truncate text-sm font-semibold text-foreground group-hover:text-primary">
            {item.title}
          </span>
        </a>
        <Badge tone={featureStatusTone(item.status)}>{item.status}</Badge>
        {item.isBlocked ? <Badge tone="danger">blocked</Badge> : null}
        <span className="ml-auto min-w-0 truncate">
          <FeatureClaimChip item={item} />
        </span>
      </div>
      <FeatureLaneActions projectId={projectId} slug={item.slug} currentLane={null} />
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

  const { data: roster, loading, error } = useLanes(projectId);
  const { data: features, loading: boardLoading, error: boardError } = usePlanningBoard(projectId);

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

  const lanes = (roster?.lanes ?? []).map((lane) => ({
    ...lane,
    features: lane.features.filter((feature) => feature.status !== "done"),
  }));
  const priorities = priorityBySlug(features);
  const unlaned = sortBoardItems(
    features.filter(
      (item) =>
        (item.lane === null || item.lane === undefined) &&
        item.status !== "done" &&
        isStatusVisible(statusFilter, item.status),
    ),
  );

  return (
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

      <ErrorText error={error ?? boardError} />

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
              priorities={priorities}
              statusFilter={statusFilter}
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
        ) : unlaned.length === 0 ? (
          <p className="text-xs text-muted">Every active feature on the board carries a lane.</p>
        ) : (
          <ul className="space-y-2">
            {unlaned.map((item) => (
              <UnlanedFeatureRow
                key={item.slug}
                projectId={projectId}
                numbering={numbering}
                item={item}
              />
            ))}
          </ul>
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
  );
}
