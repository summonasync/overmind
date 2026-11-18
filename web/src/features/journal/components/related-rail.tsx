"use client";

import { Button } from "@/shared/ui/button";
import { RailHeading } from "@/features/journal/components/backlinks-rail";
import { noteHref } from "@/features/journal/lib/note-rails";
import type { RelatedRow } from "@/features/journal/lib/note-rails";

interface RelatedRailProps {
  rows: readonly RelatedRow[];
  asked: boolean;
  loading: boolean;
  failed: boolean;
  onAsk: () => void;
  onNavigate: (href: string) => void;
}

// Passport §4's "Related by embedding" is free off the existing Qdrant index, and §7 defines
// its degraded state: with the embeddings door down the section disappears rather than
// erroring, because a note without semantic neighbours is still a readable note. So a failed
// ask renders nothing at all — the door back to it is the same button the empty-backlinks
// state carries, and the owner can press it again.
export function RelatedRail({
  rows,
  asked,
  loading,
  failed,
  onAsk,
  onNavigate,
}: RelatedRailProps) {
  if (failed) {
    return null;
  }
  if (!asked && rows.length === 0) {
    return (
      <section className="space-y-1">
        <RailHeading title="Related by embedding" />
        <Button size="xs" variant="ghost" onClick={onAsk}>
          Find related
        </Button>
      </section>
    );
  }

  return (
    <section aria-label="Related by embedding">
      <RailHeading title="Related by embedding" count={rows.length} />
      {loading ? (
        <p className="mt-1 text-xs text-muted" role="status">
          Asking the index…
        </p>
      ) : rows.length === 0 ? (
        <p className="mt-1 text-xs text-muted">The index found nothing close.</p>
      ) : (
        <ul className="mt-1 space-y-2">
          {rows.map((row) => {
            const href = noteHref(row.id);
            return (
              <li key={row.id} className="min-w-0">
                <a
                  href={href}
                  onClick={(event) => {
                    event.preventDefault();
                    onNavigate(href);
                  }}
                  className="block truncate text-sm text-foreground underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                >
                  {row.title.trim().length > 0 ? row.title : "Untitled note"}
                </a>
                {row.snippet.trim().length > 0 ? (
                  <p className="truncate text-xs text-muted">{row.snippet}</p>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
