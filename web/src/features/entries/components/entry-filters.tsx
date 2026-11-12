"use client";

import { ENTRY_TYPES } from "@/shared/api/types";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { ENTRY_STATES } from "@/features/entries/types";
import type { EntryListFilters, EntryState, EntryStateFilter } from "@/features/entries/types";

const ENTRY_STATE_LABELS: Record<EntryState, string> = {
  active: "Active",
  lifted: "Lifted",
  superseded: "Superseded",
};

interface EntryFiltersProps {
  filters: EntryListFilters;
  onChange: (filters: EntryListFilters) => void;
}

export function EntryFilters({ filters, onChange }: EntryFiltersProps) {
  const state = filters.state ?? "all";
  return (
    <div className="flex flex-wrap items-center gap-2">
      <Select
        aria-label="Filter by type"
        value={filters.type}
        onChange={(event) =>
          onChange({ ...filters, type: event.target.value as EntryListFilters["type"] })
        }
      >
        <option value="">All types</option>
        {ENTRY_TYPES.map((type) => (
          <option key={type} value={type}>
            {ENTRY_TYPE_META[type].label}
          </option>
        ))}
      </Select>
      <Select
        aria-label="Filter by state"
        value={state}
        onChange={(event) =>
          onChange({ ...filters, state: event.target.value as EntryStateFilter })
        }
      >
        <option value="all">All states</option>
        {ENTRY_STATES.map((entryState) => (
          <option key={entryState} value={entryState}>
            {ENTRY_STATE_LABELS[entryState]}
          </option>
        ))}
      </Select>
      <Input
        aria-label="Filter by tag"
        value={filters.tag}
        onChange={(event) => onChange({ ...filters, tag: event.target.value })}
        placeholder="Filter by tag"
        className="w-44"
      />
      {(filters.type || filters.tag || state !== "all") && (
        <Button variant="ghost" onClick={() => onChange({ type: "", tag: "", state: undefined })}>
          Clear
        </Button>
      )}
    </div>
  );
}
