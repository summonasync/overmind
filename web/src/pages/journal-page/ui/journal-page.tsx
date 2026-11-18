"use client";

import { Suspense, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageBackLink } from "@/shared/ui/page-back-link";
import { PageHeader } from "@/shared/ui/page-header";
import {
  JournalFilters,
  JournalStream,
  journalServerFilters,
  selectJournalDay,
  useJournalContainer,
  useJournalFilters,
  useJournalLinkAccents,
  useJournalMutations,
  useJournalNote,
  useJournalStream,
  useTagColorMap,
} from "@/features/journal";
import type { JournalNote } from "@/features/journal";

// The stream branch is T7's; the note branch is still T5's placeholder and is replaced
// whole by T8.

function NotePlaceholder({ noteId }: { noteId: string }) {
  const { data, isPending, error } = useJournalNote(noteId);

  if (isPending) {
    return <LoadingState label="Loading note…" />;
  }
  if (error) {
    return <ErrorText error={error} />;
  }
  return (
    <div className="space-y-3">
      <PageBackLink href="/journal" label="Journal" />
      <h1 className="text-2xl font-semibold text-foreground">{data.title || "(untitled)"}</h1>
      <p className="text-xs text-muted font-mono">
        {data.createdAt} · v{data.currentVersion} · {data.authorAlias ?? "you"}
      </p>
      <p className="text-xs text-muted font-mono">
        {data.outgoing.length} outgoing · {data.incoming.length} backlinks · {data.unresolved.length} unresolved
      </p>
      <p className="text-sm text-foreground whitespace-pre-wrap">{data.content}</p>
    </div>
  );
}

function JournalStreamBranch({ containerId }: { containerId: string }) {
  const router = useRouter();
  const { filters, setFilter, clear } = useJournalFilters();
  const server = useMemo(() => journalServerFilters(filters), [filters]);
  const stream = useJournalStream(containerId, server);
  const tagColors = useTagColorMap();
  const notes = useMemo(
    () => selectJournalDay(stream.data, filters.day),
    [filters.day, stream.data],
  );
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
    <div className="flex items-start gap-6">
      <JournalFilters filters={filters} onChange={setFilter} onClear={clear} />
      <div className="min-w-0 max-w-3xl flex-1 space-y-2">
        {accents.cohortFailed ? (
          <p className="text-xs text-warning" role="status">
            The server could not say which notes are linked, so the colour coding is off
            for now. The list below is still the real one.
          </p>
        ) : null}
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
  const searchParams = useSearchParams();
  const noteId = searchParams?.get("note") ?? "";
  const { containerId, loading: containerLoading, error: containerError } = useJournalContainer();

  if (noteId.length > 0) {
    return (
      <div className="space-y-4">
        <NotePlaceholder noteId={noteId} />
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
      <PageHeader title="Journal" description="Your notes, links and days." />
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
