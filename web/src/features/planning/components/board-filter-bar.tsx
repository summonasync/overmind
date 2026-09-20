"use client";

import { EyeOff, Pin, X } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { Select } from "@/shared/ui/select";
import { cn } from "@/shared/lib/cn";
import { BOARD_ARTIFACT_FILTERS } from "@/features/planning/lib/board-filters";
import { useBoardFilters } from "@/features/planning/lib/use-board-filters";
import { PLANNING_FEATURE_STATUSES } from "@/features/planning/types";
import type {
  PlanningBoardArtifactFilter,
  PlanningBoardFilters,
} from "@/features/planning/lib/board-filters";
import type { PlanningFeatureStatus } from "@/features/planning/types";

const ARTIFACT_FILTER_LABELS: Record<PlanningBoardArtifactFilter, string> = {
  any: "any",
  validated: "validated",
  notValidated: "not validated",
  none: "none",
};

const chipBase =
  "inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs font-medium capitalize transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40";
const chipInactive = "border-border text-muted hover:border-border-strong hover:text-foreground";
const chipActive = "border-primary bg-primary/10 text-primary";

export function BoardFilterBar() {
  const { filters, isActive, setFilters, clearFilters } = useBoardFilters();

  const toggleStatus = (status: PlanningFeatureStatus) => {
    const statuses = filters.statuses.includes(status)
      ? filters.statuses.filter((item) => item !== status)
      : [...filters.statuses, status];
    setFilters({ ...filters, statuses });
  };

  const setArtifactFilter = (
    key: "spec" | "plan",
    value: PlanningBoardArtifactFilter,
  ) => {
    const next: PlanningBoardFilters = { ...filters, [key]: value };
    setFilters(next);
  };

  return (
    <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-surface p-3">
      <span className="text-xs font-medium text-muted">Filters</span>

      <div role="group" aria-label="Filter by feature status" className="flex flex-wrap items-center gap-1.5">
        {PLANNING_FEATURE_STATUSES.map((status) => (
          <button
            key={status}
            type="button"
            aria-pressed={filters.statuses.includes(status)}
            onClick={() => toggleStatus(status)}
            className={cn(
              chipBase,
              filters.statuses.includes(status) ? chipActive : chipInactive,
            )}
          >
            {status}
          </button>
        ))}
      </div>

      <label className="flex items-center gap-1.5 text-xs text-muted">
        Spec
        <Select
          aria-label="Spec filter"
          value={filters.spec}
          onChange={(event) =>
            setArtifactFilter("spec", event.target.value as PlanningBoardArtifactFilter)
          }
        >
          {BOARD_ARTIFACT_FILTERS.map((filter) => (
            <option key={filter} value={filter}>
              {ARTIFACT_FILTER_LABELS[filter]}
            </option>
          ))}
        </Select>
      </label>

      <label className="flex items-center gap-1.5 text-xs text-muted">
        Plan
        <Select
          aria-label="Plan filter"
          value={filters.plan}
          onChange={(event) =>
            setArtifactFilter("plan", event.target.value as PlanningBoardArtifactFilter)
          }
        >
          {BOARD_ARTIFACT_FILTERS.map((filter) => (
            <option key={filter} value={filter}>
              {ARTIFACT_FILTER_LABELS[filter]}
            </option>
          ))}
        </Select>
      </label>

      <button
        type="button"
        aria-pressed={filters.currentOnly}
        onClick={() => setFilters({ ...filters, currentOnly: !filters.currentOnly })}
        className={cn(chipBase, filters.currentOnly ? chipActive : chipInactive)}
      >
        <Pin size={12} aria-hidden />
        Current only
      </button>

      <button
        type="button"
        aria-pressed={filters.hideDone}
        onClick={() => setFilters({ ...filters, hideDone: !filters.hideDone })}
        className={cn(chipBase, filters.hideDone ? chipActive : chipInactive)}
      >
        <EyeOff size={12} aria-hidden />
        Hide done
      </button>

      {isActive ? (
        <Button variant="ghost" className="ml-auto px-2 py-1 text-xs" onClick={clearFilters}>
          <X size={12} aria-hidden />
          Clear filters
        </Button>
      ) : null}
    </div>
  );
}
