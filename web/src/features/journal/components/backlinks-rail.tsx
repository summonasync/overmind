"use client";

import { Button } from "@/shared/ui/button";
import { noteHref, dayHref } from "@/features/journal/lib/note-rails";
import { localDayOf } from "@/features/journal/lib/stream-filters";
import type { JournalBacklink } from "@/features/journal/types";

export const NO_BACKLINKS_LINE = "Nobody links here yet.";

interface BacklinksRailProps {
  backlinks: readonly JournalBacklink[];
  askRelated: () => void;
  askingRelated: boolean;
  onNavigate: (href: string) => void;
}

// Passport §4 names this panel the single highest-value affordance of the feature, so it is
// the one section that never collapses and never hides behind a click. Each row is the
// source title at `text-sm` over one line of the Host's own context at `text-xs text-muted`.
//
// The context line is `snippet` from the detail payload — the Host cuts it around the marker
// (R7), and this surface does not re-read note bodies to build a second one.
export function BacklinksRail({
  backlinks,
  askRelated,
  askingRelated,
  onNavigate,
}: BacklinksRailProps) {
  if (backlinks.length === 0) {
    // Passport §7's empty-backlinks state: a muted line plus the embeddings door, never a
    // spinner and never an error — an unlinked note is a normal note.
    return (
      <section aria-label="Backlinks" className="space-y-2">
        <RailHeading title="Backlinks" count={0} />
        <p className="text-xs text-muted">{NO_BACKLINKS_LINE}</p>
        <Button size="xs" variant="ghost" onClick={askRelated} disabled={askingRelated}>
          {askingRelated ? "Asking…" : "Suggest related"}
        </Button>
      </section>
    );
  }

  return (
    <section aria-label="Backlinks">
      <RailHeading title="Backlinks" count={backlinks.length} />
      <ul className="mt-1 space-y-2">
        {backlinks.map((link) => {
          const href = noteHref(link.entryId);
          return (
            <li key={link.entryId} className="min-w-0">
              <a
                href={href}
                onClick={(event) => {
                  // The router keeps the note branch a query-param swap; the href stays a
                  // real link for middle-click and for copy-address.
                  event.preventDefault();
                  onNavigate(href);
                }}
                className="block truncate text-sm text-foreground underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
              >
                {link.title.trim().length > 0 ? link.title : "Untitled note"}
              </a>
              {link.snippet.trim().length > 0 ? (
                <p className="truncate text-xs text-muted">{link.snippet}</p>
              ) : null}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

const RAIL_HEADING_CLASSES = "text-xs font-semibold uppercase tracking-wide text-muted";

export function RailHeading({ title, count }: { title: string; count?: number }) {
  return (
    <h2 className={RAIL_HEADING_CLASSES}>
      {title}
      {count === undefined ? null : ` (${count})`}
    </h2>
  );
}

// The rail's day row: which local day this note last moved on, spelled by the one day key the
// stream uses (spec R5's amended clause) and linking to that day's shelf in the inbox.
export function RailDayRow({
  updatedAt,
  onNavigate,
}: {
  updatedAt: string;
  onNavigate: (href: string) => void;
}) {
  const day = localDayOf(updatedAt);
  if (day === null) {
    return null;
  }
  const href = dayHref(day);
  return (
    <a
      href={href}
      onClick={(event) => {
        event.preventDefault();
        onNavigate(href);
      }}
      className="text-xs text-muted font-mono underline-offset-2 hover:text-foreground hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
    >
      {day}
    </a>
  );
}
