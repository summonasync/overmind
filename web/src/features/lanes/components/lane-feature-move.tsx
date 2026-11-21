"use client";

import { ArrowDown, ArrowUp } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import type { PlanningFeaturePriority } from "@/features/planning";
import type { LaneMoveTarget } from "@/features/lanes/lib/lane-order";
import type { LaneMoveController } from "@/features/lanes/lib/use-feature-queue-move";

interface LaneFeatureMoveProps {
  slug: string;
  laneName: string;
  priority: PlanningFeaturePriority;
  target: LaneMoveTarget;
  queueMove: LaneMoveController;
}

const buttonClasses = cn(
  "shrink-0 rounded p-1 text-muted transition-colors",
  "hover:bg-primary/10 hover:text-primary",
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
  "disabled:cursor-not-allowed disabled:opacity-40",
);

const positionClasses = cn("font-mono text-[11px] tabular-nums text-muted");

export function LaneFeatureMove({
  slug,
  laneName,
  priority,
  target,
  queueMove,
}: LaneFeatureMoveProps) {
  const requestMove = (position: number | null) => {
    if (position === null) {
      return;
    }
    queueMove.move(slug, position);
  };

  // The mate is one of the cards this column actually shows; the number it carries is the full-band slot,
  // so a filter changes who the move aims at but never which slot goes on the wire.
  const mateHint = (position: number | null, direction: string) =>
    position === null
      ? `No ${priority} ${direction} it in ${laneName}`
      : `Move onto the ${priority} ${direction} it, band slot ${position}`;

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1">
      <button
        type="button"
        aria-label={`Move ${slug} up in ${laneName}`}
        title={mateHint(target.upTo, "lane-mate above")}
        disabled={queueMove.busy || target.upTo === null}
        onClick={() => requestMove(target.upTo)}
        className={buttonClasses}
      >
        <ArrowUp size={12} aria-hidden />
      </button>
      <button
        type="button"
        aria-label={`Move ${slug} down in ${laneName}`}
        title={mateHint(target.downTo, "lane-mate below")}
        disabled={queueMove.busy || target.downTo === null}
        onClick={() => requestMove(target.downTo)}
        className={buttonClasses}
      >
        <ArrowDown size={12} aria-hidden />
      </button>
      <span className={positionClasses} title={`Band ${priority} slot ${target.position}`}>
        {target.position}
        <span className="sr-only">{` — band ${priority}`}</span>
      </span>
    </div>
  );
}
