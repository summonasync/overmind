"use client";

import { useState } from "react";
import { Tags } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { Modal } from "@/shared/ui/modal";
import { TagColorPalette } from "@/features/entries/components/tag-color-palette";
import { useTagColorUpdate, useTagList } from "@/features/entries/lib/use-tags";
import type { TagResponse } from "@/features/entries/types";

interface TagManagerRowProps {
  tag: TagResponse;
  pending: boolean;
  onPick: (color: string) => void;
}

function TagManagerRow({ tag, pending, onPick }: TagManagerRowProps) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-3">
      <div className="flex min-w-0 items-center gap-2">
        <span
          aria-hidden
          className="h-3 w-3 shrink-0 rounded-full border border-border-strong"
          style={tag.color ? { backgroundColor: tag.color } : undefined}
        />
        <span className="truncate text-sm font-medium text-foreground">{tag.name}</span>
      </div>
      <TagColorPalette tagName={tag.name} value={tag.color} disabled={pending} onPick={onPick} />
    </li>
  );
}

export function TagManager() {
  const [open, setOpen] = useState(false);
  const { data: tags, isPending, error } = useTagList();
  const setColor = useTagColorUpdate();

  return (
    <>
      <Button variant="secondary" onClick={() => setOpen(true)}>
        <Tags size={14} aria-hidden />
        Tags
      </Button>

      <Modal title="Tags" open={open} onClose={() => setOpen(false)}>
        {isPending ? (
          <LoadingState label="Loading tags…" />
        ) : error ? (
          <ErrorText error={error as Error} />
        ) : tags.length === 0 ? (
          <EmptyState
            title="No tags yet"
            hint="Tag entries first — every tag shows up here for color coding."
          />
        ) : (
          <div className="space-y-2">
            <ErrorText error={setColor.error as Error | null} />
            <ul className="divide-y divide-border">
              {tags.map((tag) => (
                <TagManagerRow
                  key={tag.id}
                  tag={tag}
                  pending={setColor.isPending && setColor.variables?.id === tag.id}
                  onPick={(color) => setColor.mutate({ id: tag.id, color })}
                />
              ))}
            </ul>
          </div>
        )}
      </Modal>
    </>
  );
}
