"use client";

import { ArrowRight, Clock, Lightbulb, NotebookText } from "lucide-react";
import {
  InfoPanelCollapsibleSection,
  InfoPanelSection,
  InfoPanelStat,
} from "@/widgets/app-shell";
import { formatCalendarDate } from "@/shared/lib/relative-time";
import { ENTRY_TYPES } from "@/shared/api/types";
import type { EntryResponse } from "@/features/entries";

const TIPS = [
  "Use the filters to narrow entries by type or tag.",
  "Skills can bundle per-platform files for agent setups.",
  "Run a reindex on the Settings page after bulk edits to keep semantic search fresh.",
] as const;

interface EntriesInfoPanelProps {
  entries: EntryResponse[];
  loading: boolean;
}

export function EntriesInfoPanel({ entries, loading }: EntriesInfoPanelProps) {
  if (loading) {
    return null;
  }

  const tagged = entries.filter((entry) => entry.tags.length > 0).length;
  const recent = [...entries]
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
    .slice(0, 5);

  return (
    <div className="space-y-6">
      <InfoPanelCollapsibleSection
        page="entries"
        section="stats"
        title="Entries Overview"
        icon={NotebookText}
      >
        <div className="grid grid-cols-2 gap-2">
          <InfoPanelStat value={entries.length} label="Total Entries" />
          <InfoPanelStat value={tagged} label="With Tags" />
        </div>
      </InfoPanelCollapsibleSection>

      <InfoPanelSection title="By Type">
        <ul className="space-y-1.5">
          {ENTRY_TYPES.map((type) => (
            <li key={type} className="flex items-center justify-between text-xs">
              <span className="capitalize text-muted">{type}</span>
              <span className="font-medium text-foreground">
                {entries.filter((entry) => entry.type === type).length}
              </span>
            </li>
          ))}
        </ul>
      </InfoPanelSection>

      <InfoPanelSection title="Recently Updated" icon={Clock}>
        {recent.length === 0 ? (
          <p className="text-xs text-muted">No entries yet.</p>
        ) : (
          <ul className="space-y-1">
            {recent.map((entry) => (
              <li
                key={entry.id}
                className="flex items-center justify-between gap-2 rounded-md bg-surface px-2.5 py-1.5"
              >
                <span className="truncate text-xs text-foreground">{entry.title}</span>
                <span className="shrink-0 font-mono text-[10px] text-muted">
                  {formatCalendarDate(entry.updatedAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </InfoPanelSection>

      <InfoPanelCollapsibleSection page="entries" section="tips" title="Tips" icon={Lightbulb}>
        <ul className="space-y-1.5">
          {TIPS.map((tip) => (
            <li key={tip} className="flex items-start gap-1.5 text-xs leading-relaxed text-muted">
              <ArrowRight size={12} aria-hidden className="mt-0.5 shrink-0 text-primary" />
              {tip}
            </li>
          ))}
        </ul>
      </InfoPanelCollapsibleSection>
    </div>
  );
}
