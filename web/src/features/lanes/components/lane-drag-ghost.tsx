"use client";

import { Badge } from "@/shared/ui/badge";
import { featureStatusTone, priorityTone } from "@/features/planning";
import type { PlanningFeaturePriority, PlanningFeatureStatus } from "@/features/planning";

export interface LaneDragGhostCard {
  slug: string;
  number: string | null;
  title: string | null;
  status: PlanningFeatureStatus;
  priority?: PlanningFeaturePriority;
  blocked: boolean;
}

interface LaneDragGhostProps {
  card: LaneDragGhostCard;
}

export function LaneDragGhost({ card }: LaneDragGhostProps) {
  const label = card.title ?? card.slug;
  return (
    <div
      data-lane-drag-ghost={card.slug}
      aria-hidden="true"
      className="pointer-events-none w-72 scale-105 rounded-lg border border-primary/60 bg-surface p-3 opacity-90 shadow-lg"
    >
      <div className="flex min-w-0 items-center gap-2">
        {card.number !== null ? (
          <span className="shrink-0 font-mono text-sm font-semibold tracking-wide text-primary">
            {card.number}
          </span>
        ) : null}
        <span className="min-w-0 truncate text-sm font-semibold text-foreground">{label}</span>
        {card.priority !== undefined ? (
          <Badge tone={priorityTone(card.priority)}>{card.priority}</Badge>
        ) : null}
        <Badge tone={featureStatusTone(card.status)}>{card.status}</Badge>
        {card.blocked ? <Badge tone="danger">blocked</Badge> : null}
      </div>
    </div>
  );
}
