"use client";

import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import type { EntryResponse } from "@/features/entries/types";

interface EntryListProps {
  entries: EntryResponse[];
  onEdit: (entry: EntryResponse) => void;
  onDelete: (entry: EntryResponse) => void;
}

export function EntryList({ entries, onEdit, onDelete }: EntryListProps) {
  if (entries.length === 0) {
    return <EmptyState title="No entries" hint="Create an entry to capture context for this project." />;
  }

  return (
    <ul className="divide-y divide-border overflow-hidden rounded-lg border border-border bg-surface">
      {entries.map((entry) => {
        const meta = ENTRY_TYPE_META[entry.type];
        const Icon = meta.icon;
        return (
          <li key={entry.id} className="px-4 py-3 transition-colors hover:bg-surface-raised">
            <div className="flex items-center justify-between gap-4">
              <div className="flex min-w-0 items-center gap-2">
                <Icon size={15} aria-hidden className="shrink-0" style={{ color: `var(--color-type-${entry.type})` }} />
                <Badge tone={meta.tone}>{entry.type}</Badge>
                <span className="truncate text-sm font-semibold text-foreground">{entry.title}</span>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                <span className="font-mono text-xs text-muted">v{entry.currentVersion}</span>
                <Button variant="secondary" onClick={() => onEdit(entry)}>
                  Edit
                </Button>
                <Button variant="danger" onClick={() => onDelete(entry)}>
                  Delete
                </Button>
              </div>
            </div>
            {entry.tags.length > 0 ? (
              <div className="mt-1 flex flex-wrap gap-1">
                {entry.tags.map((tag) => (
                  <Badge key={tag}>{tag}</Badge>
                ))}
              </div>
            ) : null}
            <p className="mt-1 line-clamp-2 whitespace-pre-wrap text-xs text-muted">
              {entry.content}
            </p>
          </li>
        );
      })}
    </ul>
  );
}
