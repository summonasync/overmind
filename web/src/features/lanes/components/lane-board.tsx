"use client";

import { useSearchParams } from "next/navigation";
import { Badge } from "@/shared/ui/badge";
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
import type { PlanningFeatureBoardItem } from "@/features/planning";
import type { ProjectNumbering } from "@/shared/lib/feature-number";
import { useProjectNumbering } from "@/features/projects";
import { LaneCard } from "@/features/lanes/components/lane-card";
import { useLanes } from "@/features/lanes/lib/use-lanes";

const LANES_DESCRIPTION =
  "Wave view of the planning board: one card per lane with its holder, open-feature count and the features inside. Read-only — lanes are managed through the planning lanes API.";

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
    <li className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-surface px-3 py-2">
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
    </li>
  );
}

export function LaneBoard() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";
  const numbering = useProjectNumbering(projectId);

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

  const lanes = roster?.lanes ?? [];
  const unlaned = features.filter((item) => item.lane === null || item.lane === undefined);

  return (
    <div className="space-y-4">
      <PageHeader title="Lanes" description={LANES_DESCRIPTION} />

      <ErrorText error={error ?? boardError} />

      {loading ? (
        <LoadingState label="Loading lanes…" />
      ) : lanes.length === 0 ? (
        <EmptyState
          title="No lanes defined"
          hint="The project roster has no lanes yet. Agents manage the roster through the planning lanes API."
        />
      ) : (
        <ul className="grid gap-3 md:grid-cols-2 2xl:grid-cols-3">
          {lanes.map((lane) => (
            <LaneCard key={lane.name} lane={lane} />
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
          <p className="text-xs text-muted">Every feature on the board carries a lane.</p>
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
    </div>
  );
}
