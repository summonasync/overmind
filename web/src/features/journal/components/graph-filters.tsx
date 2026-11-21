"use client";

import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import {
  GRAPH_ENTRY_TYPES,
  isGraphFilterActive,
  toggleGraphEntryType,
} from "@/features/journal/lib/graph-filters";
import { GRAPH_SEARCH_HINT } from "@/features/journal/lib/graph-copy";
import { useProjectList } from "@/features/projects";
import type { GraphViewFilters } from "@/features/journal/types";

interface GraphFiltersProps {
  filters: GraphViewFilters;
  onChange: (patch: Partial<GraphViewFilters>) => void;
  onClear: () => void;
}

const DEPTH_OPTIONS = ["", "1", "2", "3"] as const;

const TYPE_LABELS: Record<string, string> = {
  journal: "Notes",
  decision: "Decisions",
  idea: "Ideas",
};

// Every control here writes the URL, so the row of chips is the field's definition: what the
// canvas draws, what the legend counts and what a pasted link reopens are all the same state.
export function GraphFilters({ filters, onChange, onClear }: GraphFiltersProps) {
  const projects = useProjectList();
  const options = [...projects.data]
    .map((project) => ({ id: project.id, name: project.name }))
    .sort((a, b) => a.name.localeCompare(b.name));
  if (filters.project.length > 0 && !options.some((option) => option.id === filters.project)) {
    options.unshift({ id: filters.project, name: filters.project });
  }

  return (
    <section
      aria-label="Graph filters"
      className="flex flex-wrap items-end gap-x-4 gap-y-3 rounded-md border border-border bg-surface p-3"
    >
      <fieldset className="min-w-0">
        <legend className="text-xs font-medium text-muted">Entry types</legend>
        <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1">
          {GRAPH_ENTRY_TYPES.map((type) => (
            <label
              key={type}
              className="flex min-h-[24px] cursor-pointer items-center gap-2 text-sm text-foreground"
            >
              <input
                type="checkbox"
                className="size-4 accent-primary"
                checked={filters.types.includes(type)}
                onChange={() =>
                  onChange({ types: toggleGraphEntryType(filters.types, type) })
                }
              />
              {TYPE_LABELS[type]}
            </label>
          ))}
        </div>
      </fieldset>

      <div className="min-w-0 space-y-1">
        <label htmlFor="graph-filter-project" className="block text-xs font-medium text-muted">
          Project
        </label>
        <Select
          id="graph-filter-project"
          size="sm"
          className="min-h-[24px]"
          value={filters.project}
          onChange={(event) => onChange({ project: event.target.value })}
        >
          <option value="">All projects</option>
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name}
            </option>
          ))}
        </Select>
      </div>

      <div className="min-w-0 space-y-1">
        <label htmlFor="graph-filter-search" className="block text-xs font-medium text-muted">
          Title search
        </label>
        <Input
          id="graph-filter-search"
          className="min-h-[24px]"
          value={filters.search}
          placeholder="exact words from a title"
          title={GRAPH_SEARCH_HINT}
          onChange={(event) => onChange({ search: event.target.value })}
        />
        <p className="text-xs text-muted">{GRAPH_SEARCH_HINT}</p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button
          size="sm"
          variant={filters.orphansOnly ? "primary" : "secondary"}
          aria-pressed={filters.orphansOnly}
          onClick={() => onChange({ orphansOnly: !filters.orphansOnly, focus: "", depth: "" })}
        >
          Unlinked only
        </Button>
        <Button
          size="sm"
          variant={filters.includeDone ? "primary" : "secondary"}
          aria-pressed={filters.includeDone}
          onClick={() => onChange({ includeDone: !filters.includeDone })}
        >
          Include Done features
        </Button>
      </div>

      <div className="min-w-0 space-y-1">
        <label htmlFor="graph-filter-depth" className="block text-xs font-medium text-muted">
          Hops from centre
        </label>
        <Select
          id="graph-filter-depth"
          size="sm"
          className="min-h-[24px]"
          value={filters.depth}
          disabled={filters.focus.length === 0}
          onChange={(event) => onChange({ depth: event.target.value })}
        >
          {DEPTH_OPTIONS.map((depth) => (
            <option key={depth || "default"} value={depth}>
              {depth.length === 0 ? "One hop" : `${depth} hop${depth === "1" ? "" : "s"}`}
            </option>
          ))}
        </Select>
      </div>

      <Button
        size="sm"
        variant="ghost"
        disabled={!isGraphFilterActive(filters)}
        onClick={onClear}
      >
        Clear filters
      </Button>
    </section>
  );
}
