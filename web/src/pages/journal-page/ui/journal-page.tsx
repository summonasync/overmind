"use client";

import { Suspense, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import {
  JournalStream,
  JournalToolbar,
  NoteView,
  TagRail,
  journalServerFilters,
  sortByActivityDesc,
  useJournalContainer,
  useJournalFilters,
  useJournalLinkAccents,
  useJournalMutations,
  useJournalStream,
  useOpenCapture,
  useTagColorMap,
} from "@/features/journal";
import type { JournalNote } from "@/features/journal";

// The stream branch is T7's; the note branch is T8's `NoteView`. Spec R5 puts the detail in a
// query param (`?note=<id>`), not a path segment, so this page owns both branches and swaps
// them on one parameter.

function JournalStreamBranch({ containerId }: { containerId: string }) {
  const router = useRouter();
  const { filters, setFilter, clear } = useJournalFilters();
  const server = useMemo(() => journalServerFilters(filters), [filters]);
  const stream = useJournalStream(containerId, server);
  const tagColors = useTagColorMap();
  const notes = useMemo(() => sortByActivityDesc(stream.data), [stream.data]);
  const accents = useJournalLinkAccents(containerId, server, stream.data);
  const mutations = useJournalMutations(containerId);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const openNote = (note: JournalNote) =>
    router.push(`/journal?note=${encodeURIComponent(note.id)}`);

  const deleteNote = (note: JournalNote) => {
    setDeletingId(note.id);
    mutations.remove.mutate(
      { id: note.id, reason: "deleted from the journal inbox" },
      { onSettled: () => setDeletingId(null) },
    );
  };

  return (
    <div className="flex items-start gap-4">
      <TagRail
        containerId={containerId}
        tagColors={tagColors}
        activeTag={filters.tag}
        onSelect={(tag) => setFilter({ tag })}
      />
      <div className="min-w-0 flex-1 space-y-3">
        {accents.cohortFailed ? (
          <p className="text-xs text-warning" role="status">
            The server could not say which notes are linked, so the colour coding is off
            for now. The list below is still the real one.
          </p>
        ) : null}
        <JournalToolbar filters={filters} onChange={setFilter} onClear={clear} />
        <JournalStream
          notes={notes}
          loading={stream.loading}
          error={stream.error}
          journalMissing={containerId.length === 0}
          tagColors={tagColors}
          deletingId={deletingId}
          accentOf={accents.accentOf}
          onRetry={stream.refetch}
          onOpen={openNote}
          onDelete={deleteNote}
        />
      </div>
    </div>
  );
}

function JournalPageContent() {
  const openCapture = useOpenCapture();
  const searchParams = useSearchParams();
  const noteId = searchParams?.get("note") ?? "";
  const { containerId, loading: containerLoading, error: containerError } = useJournalContainer();

  if (noteId.length > 0) {
    return (
      <div className="space-y-4">
        <NoteView noteId={noteId} search={searchParams?.toString() ?? ""} />
      </div>
    );
  }
  if (containerLoading) {
    return (
      <div className="space-y-4">
        <PageHeader title="Journal" description="Your notes, links and days." />
        <LoadingState label="Locating the journal container…" />
      </div>
    );
  }
  if (containerError) {
    return (
      <div className="space-y-4">
        <PageHeader title="Journal" description="Your notes, links and days." />
        <ErrorText error={containerError} />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Journal"
        description="Your notes, links and days."
        actions={
          <Button size="sm" onClick={openCapture}>
            New note
          </Button>
        }
      />
      <JournalStreamBranch containerId={containerId} />
    </div>
  );
}

export function JournalPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <JournalPageContent />
    </Suspense>
  );
}
