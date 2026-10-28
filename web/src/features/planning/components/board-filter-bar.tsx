"use client";

import { BellRing, X } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { Select } from "@/shared/ui/select";
import { BoardFilterChip } from "@/features/planning/components/board-filter-chip";
import { BoardFastChips } from "@/features/planning/components/board-fast-chips";
import { BoardSearchInput } from "@/features/planning/components/board-search-input";
import {
  BOARD_ARTIFACT_FILTERS,
  toggleBoardFastChip,
  toggleBoardPriorityFilter,
  toggleBoardStatusFilter,
} from "@/features/planning/lib/board-filters";
import { useBoardFilters } from "@/features/planning/lib/use-board-filters";
import { FEATURE_PRIORITIES, PLANNING_FEATURE_STATUSES } from "@/features/planning/types";
import type {
  PlanningBoardArtifactFilter,
  PlanningBoardFilters,
} from "@/features/planning/lib/board-filters";
import type { BoardSearchController } from "@/features/planning/lib/use-board-search";

const ARTIFACT_FILTER_LABELS: Record<PlanningBoardArtifactFilter, string> = {
  any: "any",
  validated: "validated",
  notValidated: "not validated",
  none: "none",
};

interface BoardFilterBarProps {
  search: BoardSearchController;
}

export function BoardFilterBar({ search }: BoardFilterBarProps) {
  const { filters, isActive, setFilters, clearFilters } = useBoardFilters();

  const setArtifactFilter = (
    key: "spec" | "plan",
    value: PlanningBoardArtifactFilter,
  ) => {
    const next: PlanningBoardFilters = { ...filters, [key]: value };
    setFilters(next);
  };

  return (
    <div className="flex flex-col gap-2.5 rounded-lg border border-border bg-surface p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs font-medium text-muted">Filters</span>

        <div role="group" aria-label="Filter by feature status" className="flex flex-wrap items-center gap-1.5">
          {PLANNING_FEATURE_STATUSES.map((status) => (
            <BoardFilterChip
              key={status}
              active={filters.statuses.includes(status)}
              onClick={() => setFilters(toggleBoardStatusFilter(filters, status))}
            >
              {status}
            </BoardFilterChip>
          ))}
        </div>

        <div role="group" aria-label="Filter by priority" className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs font-medium text-muted">Priority</span>
          {FEATURE_PRIORITIES.map((priority) => (
            <BoardFilterChip
              key={priority}
              active={filters.priorities.includes(priority)}
              onClick={() => setFilters(toggleBoardPriorityFilter(filters, priority))}
            >
              {priority}
            </BoardFilterChip>
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

        {isActive ? (
          <Button variant="ghost" size="sm" className="ml-auto" onClick={clearFilters}>
            <X size={12} aria-hidden />
            Clear filters
          </Button>
        ) : null}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <BoardSearchInput
          value={search.draft}
          applied={search.isActive}
          onChange={search.setDraft}
          onApply={search.apply}
          onClear={search.clear}
          onClearAll={() => {
            search.clear();
            clearFilters();
          }}
        />

        <BoardFastChips
          filters={filters}
          onToggle={(chip) => setFilters(toggleBoardFastChip(filters, chip))}
        />

        <div role="group" aria-label="Require attention filter">
          <BoardFilterChip
            active={filters.attentionOnly}
            onClick={() => setFilters({ ...filters, attentionOnly: !filters.attentionOnly })}
          >
            <BellRing size={12} aria-hidden />
            Attention
            <span className="sr-only">(features where you are the next actor)</span>
          </BoardFilterChip>
        </div>
      </div>
    </div>
  );
}
