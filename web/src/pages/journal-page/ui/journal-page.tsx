"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageBackLink } from "@/shared/ui/page-back-link";
import { PageHeader } from "@/shared/ui/page-header";
import {
  journalDayWindow,
  useJournalContainer,
  useJournalNote,
  useJournalStream,
} from "@/features/journal";
import type { JournalStreamFilters } from "@/features/journal";

// Route shell only: it proves the data layer end to end (URL → filters → cache
// → server) and will be replaced component by component in T6-T8.
function StreamPlaceholder({ projectId, filters }: { projectId: string; filters: JournalStreamFilters }) {
  const { data, loading, error } = useJournalStream(projectId, filters);

  if (projectId.length === 0) {
    return (
      <p className="text-sm text-muted">
        No journal yet — the reserved Journal project is created with the first note.
      </p>
    );
  }
  if (loading) {
    return <LoadingState label="Loading journal…" />;
  }
  if (error) {
    return <ErrorText error={error} />;
  }
  if (data.length === 0) {
    return <p className="text-sm text-muted">No journal notes match this window.</p>;
  }
  return (
    <div className="space-y-2">
      <p className="text-xs text-muted font-mono">
        {filters.from ? `${filters.from} → ${filters.to} · ` : ""}
        {filters.tag ? `tag=${filters.tag} · ` : ""}
        {filters.link ? `link=${filters.link} · ` : ""}
        {filters.project ? `project=${filters.project} · ` : ""}
        {data.length} notes
      </p>
      <ul className="space-y-1">
        {data.map((note) => (
          <li key={note.id} className="text-sm text-foreground">
            {note.title || "(untitled)"}
            <span className="ml-2 text-xs text-muted font-mono">{note.updatedAt}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

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

function JournalPageContent() {
  const searchParams = useSearchParams();
  const noteId = searchParams?.get("note") ?? "";
  const day = searchParams?.get("day") ?? "";
  const dayWindow = journalDayWindow(day);
  const filters: JournalStreamFilters = {
    from: dayWindow?.from ?? "",
    to: dayWindow?.to ?? "",
    tag: searchParams?.get("tag") ?? "",
    link: searchParams?.get("link") ?? "",
    project: searchParams?.get("project") ?? "",
  };
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
      <StreamPlaceholder projectId={containerId} filters={filters} />
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
