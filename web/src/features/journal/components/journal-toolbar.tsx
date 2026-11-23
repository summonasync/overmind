"use client";

import { useEffect, useState } from "react";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { useTagList } from "@/features/entries";
import { useProjectList } from "@/features/projects";
import { isJournalFilterActive } from "@/features/journal/lib/stream-filters";
import type { JournalLinkFilter, JournalStreamFilters } from "@/features/journal/types";

const TRIAGE_OPTIONS: { value: JournalLinkFilter; label: string }[] = [
  { value: "", label: "Everything" },
  { value: "unlinked", label: "Unsorted" },
  { value: "linked", label: "Linked" },
];

interface JournalToolbarProps {
  filters: JournalStreamFilters;
  onChange: (patch: Partial<JournalStreamFilters>) => void;
  onClear: () => void;
}

export function JournalToolbar({ filters, onChange, onClear }: JournalToolbarProps) {
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

  // Every keystroke is a server round-trip (FTS) and a URL replace, so the draft waits for
  // 300ms of quiet before it becomes the query. An external q change (URL navigation, Clear)
  // replaces the draft outright — the render-phase guard below is React's endorsed recipe for
  // that, not a mirrored copy of the prop. The comparison runs against the trimmed draft:
  // the commit sends the trim, so the input's own echo must not read as an external change
  // and wipe a trailing space mid-word.
  const [draft, setDraft] = useState(filters.q);
  const [lastExternalQ, setLastExternalQ] = useState(filters.q);
  if (filters.q !== lastExternalQ) {
    setLastExternalQ(filters.q);
    if (filters.q !== draft.trim()) {
      setDraft(filters.q);
    }
  }

  useEffect(() => {
    if (draft === filters.q) {
      return;
    }
    const timer = setTimeout(() => onChange({ q: draft.trim() }), 300);
    return () => clearTimeout(timer);
  }, [draft, filters.q, onChange]);

  return (
    <div className="flex flex-wrap items-end gap-2">
      <div className="w-64">
        <label htmlFor="journal-search" className="mb-1 block text-xs font-medium text-muted">
          Search
        </label>
        <Input
          id="journal-search"
          type="search"
          placeholder="Title or content…"
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
        />
      </div>
      <div className="w-[150px]">
        <label htmlFor="journal-filter-day" className="mb-1 block text-xs font-medium text-muted">
          Day
        </label>
        <Input
          id="journal-filter-day"
          type="date"
          value={filters.day}
          onChange={(event) => onChange({ day: event.target.value })}
        />
      </div>
      <div className="w-[130px]">
        <label htmlFor="journal-filter-triage" className="mb-1 block text-xs font-medium text-muted">
          Triage
        </label>
        <Select
          id="journal-filter-triage"
          className="w-full"
          value={filters.link}
          onChange={(event) => onChange({ link: event.target.value as JournalLinkFilter })}
        >
          {TRIAGE_OPTIONS.map((option) => (
            <option key={option.label} value={option.value}>
              {option.label}
            </option>
          ))}
        </Select>
      </div>
      <div className="w-[200px]">
        <label htmlFor="journal-filter-project" className="mb-1 block text-xs font-medium text-muted">
          Linked project
        </label>
        <Select
          id="journal-filter-project"
          className="w-full"
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
      </div>
      <div className="w-[170px]">
        <label htmlFor="journal-filter-tag" className="mb-1 block text-xs font-medium text-muted">
          Tag
        </label>
        <Select
          id="journal-filter-tag"
          className="w-full"
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
      </div>
      {isJournalFilterActive(filters) ? (
        <Button size="md" variant="ghost" onClick={onClear}>
          Clear filters
        </Button>
      ) : null}
    </div>
  );
}
