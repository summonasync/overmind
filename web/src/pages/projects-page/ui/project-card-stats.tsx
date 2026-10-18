"use client";

import { useMemo } from "react";
import { Badge } from "@/shared/ui/badge";
import { featureStatusTone } from "@/features/planning/lib/status-tone";
import { usePlanningBoardOnce } from "@/features/planning/lib/use-planning";
import type { PlanningFeatureStatus } from "@/features/planning/types";

const OPEN_STATUS_ORDER: readonly PlanningFeatureStatus[] = ["draft", "ready", "inProgress", "review"];

const STATUS_LABELS: Record<PlanningFeatureStatus, string> = {
  draft: "draft",
  ready: "ready",
  inProgress: "in progress",
  review: "review",
  done: "done",
};

interface ProjectCardStatsProps {
  projectId: string;
}

export function ProjectCardStats({ projectId }: ProjectCardStatsProps) {
  const { data: board, loading, error } = usePlanningBoardOnce(projectId);

  const chips = useMemo(() => {
    const counts = new Map<PlanningFeatureStatus, number>();
    for (const feature of board) {
      if (feature.status === "done") {
        continue;
      }
      counts.set(feature.status, (counts.get(feature.status) ?? 0) + 1);
    }
    return OPEN_STATUS_ORDER.filter((status) => counts.has(status)).map((status) => ({
      status,
      count: counts.get(status) ?? 0,
    }));
  }, [board]);

  if (loading || error || chips.length === 0) {
    return null;
  }

  return (
    <span className="mt-auto flex flex-wrap gap-1 pt-0.5">
      {chips.map(({ status, count }) => (
        <Badge key={status} tone={featureStatusTone(status)}>
          {count} {STATUS_LABELS[status]}
        </Badge>
      ))}
    </span>
  );
}
