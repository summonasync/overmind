"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { MarkdownViewer } from "@/shared/ui/markdown-viewer";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import { EntryStateChip } from "@/features/entries/components/entry-state-chip";
import { entryTagsQueryOptions } from "@/features/entries/queries";
import {
  entryReadBody,
  entryRowClasses,
  entryTagChipStyle,
  entryTitleClasses,
  toggleEntryExpanded,
  type ExpandedEntries,
} from "@/features/entries/lib/entry-read";
import type { EntryResponse } from "@/features/entries/types";

interface EntryListProps {
  entries: EntryResponse[];
  onEdit: (entry: EntryResponse) => void;
  onDelete: (entry: EntryResponse) => void;
  onRead: (entry: EntryResponse) => void;
}

export function EntryList({ entries, onEdit, onDelete, onRead }: EntryListProps) {
  const { data: tags } = useQuery(entryTagsQueryOptions());
  const [expanded, setExpanded] = useState<ExpandedEntries>(new Set());
  const colorByName = new Map<string, string | null>(
    tags?.map((tag) => [tag.name, tag.color]),
  );

  const toggle = (id: string) => {
    setExpanded((current) => toggleEntryExpanded(current, id));
  };

  if (entries.length === 0) {
    return <EmptyState title="No entries" hint="Create an entry to capture context for this project." />;
  }

  return (
    <ul className="divide-y divide-border overflow-hidden rounded-lg border border-border bg-surface">
      {entries.map((entry) => {
        const meta = ENTRY_TYPE_META[entry.type];
        const Icon = meta.icon;
        const isExpanded = expanded.has(entry.id);
        const body = entryReadBody(entry.content);
        return (
          <li key={entry.id} className={entryRowClasses(entry.type)}>
            <div
              className="flex cursor-pointer items-center justify-between gap-4"
              onClick={() => toggle(entry.id)}
            >
              <div className="flex min-w-0 items-center gap-2">
                <button
                  type="button"
                  aria-expanded={isExpanded}
                  aria-controls={`entry-body-${entry.id}`}
                  aria-label={isExpanded ? "Collapse entry" : "Expand entry"}
                  onClick={(event) => {
                    event.stopPropagation();
                    toggle(entry.id);
                  }}
                  className="rounded p-0.5 text-muted hover:bg-surface hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                >
                  {isExpanded ? <ChevronDown size={15} aria-hidden /> : <ChevronRight size={15} aria-hidden />}
                </button>
                <Icon size={15} aria-hidden className="shrink-0" style={{ color: `var(--color-type-${entry.type})` }} />
                <Badge tone={meta.tone}>{entry.type}</Badge>
                <EntryStateChip entry={entry} />
                <button
                  type="button"
                  onClick={(event) => {
                    event.stopPropagation();
                    onRead(entry);
                  }}
                  className={`truncate text-left text-sm font-semibold ${entryTitleClasses(entry.type)} underline-offset-2 hover:text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40`}
                >
                  {entry.title}
                </button>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                {entry.authorAlias ? (
                  <span className="text-xs text-muted">by {entry.authorAlias}</span>
                ) : null}
                <span className="font-mono text-xs text-muted">v{entry.currentVersion}</span>
                <Button
                  variant="secondary"
                  onClick={(event) => {
                    event.stopPropagation();
                    onEdit(entry);
                  }}
                >
                  Edit
                </Button>
                <Button
                  variant="danger"
                  onClick={(event) => {
                    event.stopPropagation();
                    onDelete(entry);
                  }}
                >
                  Delete
                </Button>
              </div>
            </div>
            {entry.tags.length > 0 ? (
              <div className="mt-1 flex flex-wrap gap-1">
                {entry.tags.map((tag) => (
                  <Badge key={tag} style={entryTagChipStyle(colorByName.get(tag) ?? null)}>
                    {tag}
                  </Badge>
                ))}
              </div>
            ) : null}
            {isExpanded ? (
              <div id={`entry-body-${entry.id}`} className="mt-2 rounded-md border border-border bg-surface px-3 py-2">
                {body !== null ? <MarkdownViewer content={body} /> : <p className="text-sm text-muted">No content yet.</p>}
              </div>
            ) : (
              <p className="mt-1 line-clamp-2 whitespace-pre-wrap text-xs text-muted">
                {entry.content}
              </p>
            )}
          </li>
        );
      })}
    </ul>
  );
}
