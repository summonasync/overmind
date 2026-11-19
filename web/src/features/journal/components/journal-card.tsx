"use client";

import { useState } from "react";
import { Trash2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { cn } from "@/shared/lib/cn";
import { entryTagChipStyle } from "@/features/entries";
import { formatJournalStamp } from "@/features/journal/lib/stream-filters";
import type { JournalLinkAccent, JournalNote } from "@/features/journal/types";

// The bar and the filter chip are the same server answer, spelled `border-l-2` plus a left
// colour. Passport §3 writes the second token as `border-info`, and that spells
// border-color on all four sides — a tinted frame around every card, not the "left accent
// bar" the section names — so the accent is spelled `border-l-*` here. Both the width and
// the colour are only safe because the generated stylesheet emits them after the card's
// `border` and `border-border`: web's cn() concatenates, so a tie would be decided by the
// compiler and could flip silently when the theme reorders. tests/journal-stream.test.mts
// measures those offsets in the build output and goes red if the order changes.
const ACCENT_CLASSES: Record<Exclude<JournalLinkAccent, null>, string> = {
  unsorted: "border-l-2 border-l-info",
  linked: "border-l-2 border-l-success",
};

interface JournalCardProps {
  note: JournalNote;
  accent: JournalLinkAccent;
  tagColors: ReadonlyMap<string, string | null>;
  deleting: boolean;
  onOpen: (note: JournalNote) => void;
  onDelete: (note: JournalNote) => void;
}

export function JournalCard({
  note,
  accent,
  tagColors,
  deleting,
  onOpen,
  onDelete,
}: JournalCardProps) {
  const [confirming, setConfirming] = useState(false);
  const label = note.title.trim() || "Untitled note";
  const body = note.content.trim();

  return (
    <li
      tabIndex={0}
      aria-label={label}
      // Passport §7 puts stream cards in the tab order, so the stop stays — but a stop with
      // no action is a lost Tab press, so Enter on the card opens the note. The target check
      // is the safety half: Enter on the Open/Delete/Keep buttons bubbles up from that
      // button, and an unguarded card handler would open the note on top of its own action.
      onKeyDown={(event) => {
        if (event.target !== event.currentTarget || event.key !== "Enter") {
          return;
        }
        onOpen(note);
      }}
      className={cn(
        "group rounded-lg border border-border bg-surface p-4 transition-colors hover:bg-surface-raised",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
        "focus-visible:ring-offset-2 focus-visible:ring-offset-background",
        accent === null ? null : ACCENT_CLASSES[accent],
      )}
    >
      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-xs text-muted font-mono">{formatJournalStamp(note.updatedAt)}</span>
        {note.tags.map((tag) => (
          <Badge key={tag} size="xs" style={entryTagChipStyle(tagColors.get(tag) ?? null)}>
            {tag}
          </Badge>
        ))}
      </div>

      {note.title.trim().length > 0 ? (
        <h3 className="mt-1 text-lg font-semibold text-foreground">{note.title}</h3>
      ) : null}

      {body.length > 0 ? (
        <p className="mt-1 text-sm text-foreground line-clamp-3">{body}</p>
      ) : null}

      <div
        className={cn(
          "mt-2 flex items-center gap-1 transition-opacity",
          confirming ? "opacity-100" : "opacity-0 group-hover:opacity-100 group-focus-within:opacity-100",
        )}
      >
        {confirming ? (
          <>
            <Button size="xs" variant="danger" disabled={deleting} onClick={() => onDelete(note)}>
              {deleting ? "Deleting…" : "Delete for good"}
            </Button>
            <Button size="xs" variant="ghost" onClick={() => setConfirming(false)}>
              Keep
            </Button>
          </>
        ) : (
          <>
            <Button size="xs" variant="ghost" onClick={() => onOpen(note)}>
              Open
            </Button>
            <Button
              size="xs"
              variant="ghost"
              aria-label={`Delete ${label}`}
              onClick={() => setConfirming(true)}
            >
              <Trash2 size={13} aria-hidden />
            </Button>
          </>
        )}
      </div>
    </li>
  );
}
