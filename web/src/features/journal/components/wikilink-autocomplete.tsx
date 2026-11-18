"use client";

import { Folder, ListChecks, Plus } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import { cn } from "@/shared/lib/cn";
import type { Anchor } from "@/features/journal/lib/note-links";
import type { LinkSuggestion } from "@/features/journal/lib/link-targets";

export const WIKILINK_LIST_ID = "journal-wikilink-listbox";

export function wikilinkOptionId(index: number): string {
  return `${WIKILINK_LIST_ID}-option-${index}`;
}

// The note row carries the glyph the inbox already paints for a journal entry — one table for
// the product, so the same thing never shows two faces in the same app.
const ICONS = {
  note: ENTRY_TYPE_META.journal.icon,
  project: Folder,
  feature: ListChecks,
  create: Plus,
} as const;

interface WikiLinkAutocompleteProps {
  anchor: Anchor | null;
  rows: readonly LinkSuggestion[];
  activeIndex: number;
  onPick: (row: LinkSuggestion) => void;
  onHover: (index: number) => void;
}

// A listbox the textarea points at with aria-activedescendant: the caret never leaves the
// field, so the editor keeps working as a text editor while the arrows walk the list. The
// list hangs inside the editor's own positioned wrapper rather than a portal — a portal to
// `document.body` would sit outside the scrolled main and start drifting the moment the
// owner scrolls while typing. Nothing renders during the static export: `anchor` only ever
// gets a value out of a real keystroke, which is a client-only event.
export function WikiLinkAutocomplete({
  anchor,
  rows,
  activeIndex,
  onPick,
  onHover,
}: WikiLinkAutocompleteProps) {
  if (anchor === null || rows.length === 0) {
    return null;
  }

  return (
    <ul
      id={WIKILINK_LIST_ID}
      role="listbox"
      aria-label="Link suggestions"
      className="absolute z-40 max-h-64 overflow-y-auto rounded-md border border-border-strong bg-surface-raised py-1 shadow-xl"
      style={{ left: anchor.left, top: anchor.top, width: anchor.width }}
    >
      {rows.map((row, index) => {
        const Icon = ICONS[row.kind];
        const active = index === activeIndex;
        return (
          <li
            key={row.key}
            id={wikilinkOptionId(index)}
            role="option"
            aria-selected={active}
            // preventDefault on mousedown is the whole trick behind focus: without it the
            // textarea blurs before the click lands, and the caret the insert is written
            // around is gone by the time the pick runs.
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => onPick(row)}
            onMouseMove={() => onHover(index)}
            className={cn(
              "flex cursor-pointer items-center gap-2 px-2.5 py-1.5 text-sm",
              active
                ? "bg-primary/10 text-foreground"
                : "text-muted hover:bg-surface hover:text-foreground",
            )}
          >
            <Icon size={14} aria-hidden className="shrink-0" />
            <span className="min-w-0 flex-1 truncate">{row.label}</span>
            {row.createsNote ? (
              <Badge size="xs" tone="info">
                creates new note
              </Badge>
            ) : null}
            {row.kind === "note" || row.createsNote ? (
              <span className="shrink-0 text-xs text-muted">{row.detail}</span>
            ) : (
              <span className="shrink-0 truncate text-xs text-muted font-mono">{row.marker}</span>
            )}
          </li>
        );
      })}
    </ul>
  );
}
