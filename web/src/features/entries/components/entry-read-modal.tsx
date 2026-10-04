"use client";

import { useQuery } from "@tanstack/react-query";
import { Pencil } from "lucide-react";
import { Markdown } from "@/shared/lib/markdown";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import { formatCalendarDate, formatRelativeTime } from "@/shared/lib/relative-time";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Modal } from "@/shared/ui/modal";
import { entryTagsQueryOptions } from "@/features/entries/queries";
import {
  entryReadBody,
  entryReadMetaLine,
  entryTagChipStyle,
} from "@/features/entries/lib/entry-read";
import type { EntryResponse } from "@/features/entries/types";

interface EntryReadModalProps {
  entry: EntryResponse | null;
  onEdit: (entry: EntryResponse) => void;
  onClose: () => void;
}

export function EntryReadModal({ entry, onEdit, onClose }: EntryReadModalProps) {
  const { data: tags } = useQuery({ ...entryTagsQueryOptions(), enabled: entry !== null });
  const colorByName = new Map<string, string | null>(
    tags?.map((tag) => [tag.name, tag.color]),
  );
  const body = entry !== null ? entryReadBody(entry.content) : null;

  return (
    <Modal title={entry?.title ?? ""} open={entry !== null} onClose={onClose} size="xl">
      {entry ? (
        <div className="flex h-full flex-col gap-3">
          <div className="flex flex-wrap items-center gap-1.5">
            <Badge tone={ENTRY_TYPE_META[entry.type].tone}>{entry.type}</Badge>
            {entry.tags.map((tag) => (
              <Badge key={tag} style={entryTagChipStyle(colorByName.get(tag) ?? null)}>
                {tag}
              </Badge>
            ))}
            <span className="ml-auto font-mono text-xs text-muted">
              {entryReadMetaLine(
                entry.currentVersion,
                formatCalendarDate(entry.createdAt),
                formatRelativeTime(entry.updatedAt),
              )}
            </span>
            <Button variant="secondary" className="px-2.5 py-1 text-xs" onClick={() => onEdit(entry)}>
              <Pencil size={12} aria-hidden />
              Edit
            </Button>
          </div>
          <div className="min-h-32 flex-1 overflow-y-auto rounded-md border border-border bg-surface px-3 py-2">
            {body !== null ? <Markdown content={body} /> : <p className="text-sm text-muted">No content yet.</p>}
          </div>
        </div>
      ) : null}
    </Modal>
  );
}
