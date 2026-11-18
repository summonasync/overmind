"use client";

import type { ReactNode } from "react";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { useTagList } from "@/features/entries";
import { useProjectList } from "@/features/projects";
import { isJournalFilterActive } from "@/features/journal/lib/stream-filters";
import type { JournalLinkFilter, JournalStreamFilters } from "@/features/journal/types";

interface FilterGroupProps {
  id: string;
  label: string;
  children: ReactNode;
}

function FilterGroup({ id, label, children }: FilterGroupProps) {
  return (
    <div className="space-y-1">
      <label htmlFor={id} className="block text-xs font-medium text-muted">
        {label}
      </label>
      {children}
    </div>
  );
}

const TRIAGE_OPTIONS: { value: JournalLinkFilter; label: string }[] = [
  { value: "", label: "Everything" },
  { value: "unlinked", label: "Unsorted" },
  { value: "linked", label: "Linked" },
];

interface JournalFiltersProps {
  filters: JournalStreamFilters;
  onChange: (patch: Partial<JournalStreamFilters>) => void;
  onClear: () => void;
}

export function JournalFilters({ filters, onChange, onClear }: JournalFiltersProps) {
  const tags = useTagList();
  const projects = useProjectList();
  const tagNames = tags.data.map((tag) => tag.name).sort((a, b) => a.localeCompare(b));
  if (filters.tag.length > 0 && !tagNames.includes(filters.tag)) {
    tagNames.unshift(filters.tag);
  }
  const options = projects.data
    .map((project) => ({ id: project.id, name: project.name }))
    .sort((a, b) => a.name.localeCompare(b.name));
  // A URL can name a project the registry no longer has. The chip still has to show what
  // was asked for, otherwise the filter reads as unset while the server keeps honouring it.
  if (filters.project.length > 0 && !options.some((option) => option.id === filters.project)) {
    options.unshift({ id: filters.project, name: filters.project });
  }

  return (
    <aside className="w-[200px] shrink-0 space-y-4">
      <FilterGroup id="journal-filter-day" label="Day">
        <Input
          id="journal-filter-day"
          type="date"
          value={filters.day}
          onChange={(event) => onChange({ day: event.target.value })}
        />
      </FilterGroup>

      <FilterGroup id="journal-filter-triage" label="Triage">
        <Select
          id="journal-filter-triage"
          size="sm"
          value={filters.link}
          onChange={(event) => onChange({ link: event.target.value as JournalLinkFilter })}
        >
          {TRIAGE_OPTIONS.map((option) => (
            <option key={option.label} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
      </FilterGroup>

      <FilterGroup id="journal-filter-project" label="Linked project">
        <Select
          id="journal-filter-project"
          size="sm"
          value={filters.project}
          onChange={(event) => onChange({ project: event.target.value })}
        >
          <option value="">Any project</option>
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name}
            </option>
          ))}
        </Select>
      </FilterGroup>

      <FilterGroup id="journal-filter-tag" label="Tag">
        <Select
          id="journal-filter-tag"
          size="sm"
          value={filters.tag}
          onChange={(event) => onChange({ tag: event.target.value })}
        >
          <option value="">Any tag</option>
          {tagNames.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </Select>
      </FilterGroup>

      {isJournalFilterActive(filters) ? (
        <Button size="xs" variant="ghost" onClick={onClear}>
          Clear filters
        </Button>
      ) : null}
    </aside>
  );
}
