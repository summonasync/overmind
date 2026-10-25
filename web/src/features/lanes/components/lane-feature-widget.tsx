"use client";

import type { ReactNode, SyntheticEvent } from "react";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { featureStatusTone, priorityTone } from "@/features/planning";
import type { PlanningFeaturePriority, PlanningFeatureStatus } from "@/features/planning";
import { LaneWidgetMenu } from "@/features/lanes/components/lane-widget-menu";
import type { LaneClaimState } from "@/features/lanes/types";

interface LaneFeatureWidgetProps {
  slug: string;
  number: string | null;
  title: string | null;
  status: PlanningFeatureStatus;
  priority?: PlanningFeaturePriority;
  blocked?: boolean;
  taskDone?: number;
  taskTotal?: number;
  href?: string | null;
  claim?: ReactNode;
  claimAlias?: string | null;
  claimState?: LaneClaimState | null;
  children?: ReactNode;
}

const KEY_CLASSES = "font-mono text-sm font-semibold tracking-wide text-primary";

const keepPressLocal = (event: SyntheticEvent) => event.stopPropagation();

export function LaneFeatureWidget({
  slug,
  number,
  title,
  status,
  priority,
  blocked = false,
  taskDone,
  taskTotal,
  href = null,
  claim,
  claimAlias,
  claimState,
  children,
}: LaneFeatureWidgetProps) {
  const label = title ?? slug;
  const key =
    number !== null ? (
      href === null ? (
        <span className={cn("shrink-0", KEY_CLASSES)}>{number}</span>
      ) : (
        <a
          href={href}
          onPointerDown={keepPressLocal}
          className="shrink-0 rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <span className={cn(KEY_CLASSES, "hover:text-primary/80")}>{number}</span>
        </a>
      )
    ) : null;
  const percent =
    taskDone === undefined || !taskTotal ? null : Math.round((taskDone / taskTotal) * 100);

  return (
    <div
      data-lane-feature-widget={slug}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-3 transition-colors hover:border-border-strong"
    >
      <div className="flex min-w-0 items-center gap-2">
        {key}
        <span className="min-w-0 truncate text-sm font-semibold text-foreground">{label}</span>
        {priority !== undefined ? <Badge tone={priorityTone(priority)}>{priority}</Badge> : null}
        <Badge tone={featureStatusTone(status)}>{status}</Badge>
        {blocked ? <Badge tone="danger">blocked</Badge> : null}
        {claim !== undefined ? <span className="min-w-0 truncate">{claim}</span> : null}
        {href !== null ? (
          <LaneWidgetMenu slug={slug} href={href} claimAlias={claimAlias} claimState={claimState} />
        ) : null}
      </div>

      {percent !== null && taskDone !== undefined && taskTotal !== undefined ? (
        <div className="space-y-1">
          <div className="flex items-center justify-between text-xs text-muted">
            <span>
              {taskDone}/{taskTotal} done
            </span>
            <span>{percent}%</span>
          </div>
          <div
            className="h-1 overflow-hidden rounded-full bg-surface-raised"
            role="progressbar"
            aria-valuenow={percent}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-label={`Tasks done for ${label}`}
          >
            <div className="h-full bg-primary" style={{ width: `${percent}%` }} />
          </div>
        </div>
      ) : null}

      {children !== undefined ? (
        <div
          data-lane-widget-actions
          className="flex min-w-0 flex-wrap items-center gap-1 border-t border-border pt-2"
        >
          {children}
        </div>
      ) : null}
    </div>
  );
}
