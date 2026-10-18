"use client";

import { PLANNING_FEATURE_STATUSES } from "@/features/planning";
import type { PlanningFeatureStatus } from "@/features/planning";
import { cn } from "@/shared/lib/cn";

interface LaneStatusChipsProps {
  selected: ReadonlySet<PlanningFeatureStatus>;
  onToggle: (status: PlanningFeatureStatus) => void;
  onClear: () => void;
}

export function LaneStatusChips({ selected, onToggle, onClear }: LaneStatusChipsProps) {
  const allActive = selected.size === 0;

  return (
    <div role="group" aria-label="Status filters" className="flex flex-wrap items-center gap-1.5">
      <button
        type="button"
        aria-pressed={allActive}
        onClick={onClear}
        className={cn(
          "inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          allActive
            ? "border-primary bg-primary/10 text-primary"
            : "border-border text-muted hover:border-border-strong hover:text-foreground",
        )}
      >
        All
      </button>
      {PLANNING_FEATURE_STATUSES.map((status) => {
        const active = selected.has(status);
        return (
          <button
            key={status}
            type="button"
            aria-pressed={active}
            onClick={() => onToggle(status)}
            className={cn(
              "inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs font-medium capitalize transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
              active
                ? "border-primary bg-primary/10 text-primary"
                : "border-border text-muted hover:border-border-strong hover:text-foreground",
            )}
          >
            {status}
          </button>
        );
      })}
    </div>
  );
}
