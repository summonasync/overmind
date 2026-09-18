"use client";

import { Pencil, Pin, PinOff, Trash2 } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { featureStatusTone } from "@/features/planning/lib/status-tone";
import { formatRelativeTime } from "@/features/planning/lib/relative-time";
import type { PlanningArtifactStatus, PlanningFeatureBoardItem } from "@/features/planning/types";

interface FeatureBoardProps {
  projectId: string;
  features: PlanningFeatureBoardItem[];
  onCreate: () => void;
  onEdit: (item: PlanningFeatureBoardItem) => void;
  onDelete: (item: PlanningFeatureBoardItem) => void;
  onSetCurrent: (item: PlanningFeatureBoardItem, isCurrent: boolean) => void;
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

export function FeatureBoard({
  projectId,
  features,
  onCreate,
  onEdit,
  onDelete,
  onSetCurrent,
}: FeatureBoardProps) {
  if (features.length === 0) {
    return (
      <EmptyState
        title="No features yet"
        hint="Create the first feature to plan it as spec, tasks and result."
        action={<Button onClick={onCreate}>New feature</Button>}
      />
    );
  }

  const sortedFeatures = [...features].sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent));

  return (
    <ul className="grid gap-3 md:grid-cols-2 2xl:grid-cols-3">
      {sortedFeatures.map((item) => {
        const taskPercent = item.taskTotal > 0 ? Math.round((item.taskDone / item.taskTotal) * 100) : 0;
        return (
          <li
            key={item.slug}
            className={cn(
              "flex flex-col gap-3 rounded-lg border bg-surface p-4 transition-colors",
              item.isCurrent ? "border-primary" : "border-border hover:border-border-strong",
            )}
          >
            <div className="flex items-start justify-between gap-2">
              <a
                href={`/planning?project=${projectId}&feature=${item.slug}`}
                className="group min-w-0 rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                <span className="block truncate text-sm font-semibold text-foreground group-hover:text-primary">
                  {item.title}
                </span>
                <span className="block truncate font-mono text-xs text-muted">{item.slug}</span>
              </a>
              <div className="flex shrink-0 items-center gap-1.5">
                {item.isCurrent ? <Badge tone="info">current</Badge> : null}
                <Badge tone={featureStatusTone(item.status)}>{item.status}</Badge>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-1.5">
              <Badge>v{item.version}</Badge>
              {artifactChip("spec", item.specStatus)}
              {artifactChip("plan", item.planStatus)}
            </div>

            {item.taskTotal > 0 ? (
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
                >
                  <div className="h-full rounded-full bg-primary" style={{ width: `${taskPercent}%` }} />
                </div>
              </div>
            ) : null}

            <div className="mt-auto flex items-center justify-between gap-2 border-t border-border pt-2">
              <span className="truncate text-xs text-muted">updated {formatRelativeTime(item.updatedAt)}</span>
              <div className="flex shrink-0 gap-1">
                <Button
                  variant="ghost"
                  className="px-2 py-1 text-xs"
                  aria-pressed={item.isCurrent}
                  onClick={() => onSetCurrent(item, !item.isCurrent)}
                >
                  {item.isCurrent ? <PinOff size={12} aria-hidden /> : <Pin size={12} aria-hidden />}
                  {item.isCurrent ? "Unpin" : "Pin"}
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
  );
}
