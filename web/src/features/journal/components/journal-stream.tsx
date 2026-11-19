"use client";

import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { JournalCard } from "@/features/journal/components/journal-card";
import { formatCaptureShortcut } from "@/features/journal/lib/capture";
import type { JournalLinkAccent, JournalNote } from "@/features/journal/types";

const SKELETON_ROWS = [0, 1, 2, 3, 4];

// Passport §7 asks for skeleton rows, not a spinner: the shape of the list is already the
// answer, and five rows is what the eye reads as "the inbox is coming". The fill is the
// `--surface-raised` token itself — dropping the row to a literal 8% alpha over `--surface`
// measures a step of about one RGB point (#1a1a24 over #14141c), i.e. invisible.
function StreamSkeleton() {
  return (
    <>
      <p className="sr-only">Loading journal…</p>
      <ul aria-hidden="true" className="space-y-2">
        {SKELETON_ROWS.map((row) => (
          <li
            key={row}
            className="animate-pulse space-y-2 rounded-lg border border-border bg-surface p-4"
          >
            <div className="h-3 w-28 rounded bg-surface-raised" />
            <div className="h-4 w-2/3 rounded bg-surface-raised" />
            <div className="h-3 w-full rounded bg-surface-raised" />
          </li>
        ))}
      </ul>
    </>
  );
}

interface JournalStreamProps {
  notes: JournalNote[];
  loading: boolean;
  error: Error | null;
  journalMissing: boolean;
  tagColors: ReadonlyMap<string, string | null>;
  deletingId: string | null;
  accentOf: (noteId: string) => JournalLinkAccent;
  onRetry: () => void;
  onOpen: (note: JournalNote) => void;
  onDelete: (note: JournalNote) => void;
}

export function JournalStream({
  notes,
  loading,
  error,
  journalMissing,
  tagColors,
  deletingId,
  accentOf,
  onRetry,
  onOpen,
  onDelete,
}: JournalStreamProps) {
  // The degraded leg of passport §7 — Qdrant down, warning tint, sleeping zerling at 64px,
  // dismissible, list stays usable — is already rendered for every route by
  // widgets/degraded-banner, which the root layout puts around this page. It is not
  // re-invented here: a second banner would double the same warning. This stream has no
  // semantic dependency on the vector store anyway; only `?q=` would, and the inbox never
  // sends it.
  if (loading) {
    return <StreamSkeleton />;
  }

  if (error) {
    return (
      <div className="space-y-2">
        <ErrorText error={error} />
        <Button size="sm" variant="secondary" onClick={onRetry}>
          Retry
        </Button>
      </div>
    );
  }

  if (notes.length === 0) {
    const shortcut = formatCaptureShortcut(typeof navigator === "undefined" ? "" : navigator.userAgent);
    return journalMissing ? (
      <EmptyState
        title="Nothing in the journal yet"
        hint={`Throw a thought down — ${shortcut.open} from anywhere. The first one opens the Journal shelf.`}
      />
    ) : (
      <EmptyState
        title="No note matches these filters"
        hint={`Widen the window, or capture anyway — ${shortcut.open} works from here too.`}
      />
    );
  }

  return (
    <ul aria-label="Journal notes, newest activity first" className="space-y-2">
      {notes.map((note) => (
        <JournalCard
          key={note.id}
          note={note}
          accent={accentOf(note.id)}
          tagColors={tagColors}
          deleting={deletingId === note.id}
          onOpen={onOpen}
          onDelete={onDelete}
        />
      ))}
    </ul>
  );
}
