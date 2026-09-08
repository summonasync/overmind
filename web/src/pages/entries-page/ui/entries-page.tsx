"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { Modal } from "@/shared/ui/modal";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { EntryEditor, EntryFilters, EntryList, useEntryList, useEntryMutations } from "@/features/entries";
import type { EntryFormValues, EntryListFilters, EntryResponse } from "@/features/entries";
import { useProjectList } from "@/features/projects";

function EntriesPageContent() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";

  const { data: projects } = useProjectList();
  const project = projects.find((item) => item.id === projectId) ?? null;

  const [filters, setFilters] = useState<EntryListFilters>({ type: "", tag: "" });
  const { data: entries, loading, error } = useEntryList(projectId, filters);
  const { create, update, remove } = useEntryMutations(projectId);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<EntryResponse | null>(null);
  const [deleting, setDeleting] = useState<EntryResponse | null>(null);

  const activeError =
    (create.error as Error | null) ??
    (update.error as Error | null) ??
    (remove.error as Error | null) ??
    error;

  const handleSubmit = (values: EntryFormValues) => {
    if (editing) {
      update.mutate({ id: editing.id, values }, { onSuccess: () => setFormOpen(false) });
    } else {
      create.mutate({ type: values.type, values }, { onSuccess: () => setFormOpen(false) });
    }
  };

  if (!projectId) {
    return (
      <div className="space-y-4">
        <PageHeader title="Entries" description="Capture knowledge, decisions, skills and more. Turn experience into a lasting advantage." />
        <p className="text-sm text-muted">
          Select a project first.{" "}
          <a href="/" className="text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40">
            Go to Projects
          </a>
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={project ? `Entries — ${project.name}` : "Entries"}
        description="Capture knowledge, decisions, skills and more. Turn experience into a lasting advantage."
        actions={
          <>
            <a
              href="/"
              className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
            >
              ← All projects
            </a>
            <Button onClick={() => { setEditing(null); setFormOpen(true); }}>New entry</Button>
          </>
        }
      />

      <EntryFilters filters={filters} onChange={setFilters} />
      <ErrorText error={activeError} />

      {loading ? (
        <LoadingState label="Loading entries…" />
      ) : (
        <EntryList
          entries={entries}
          onEdit={(entry) => { setEditing(entry); setFormOpen(true); }}
          onDelete={setDeleting}
        />
      )}

      <Modal
        title={editing ? "Edit entry" : "New entry"}
        open={formOpen}
        onClose={() => setFormOpen(false)}
        size="xl"
      >
        <EntryEditor
          initial={editing ?? undefined}
          pending={create.isPending || update.isPending}
          error={(create.error as Error | null) ?? (update.error as Error | null)}
          onSubmit={handleSubmit}
          onCancel={() => setFormOpen(false)}
        />
      </Modal>

      <Modal title="Delete entry" open={deleting !== null} onClose={() => setDeleting(null)}>
        {deleting ? (
          <div className="space-y-4">
            <p className="text-sm text-foreground">
              Delete entry &quot;{deleting.title}&quot;? This cannot be undone.
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setDeleting(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                disabled={remove.isPending}
                onClick={() => remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })}
              >
                {remove.isPending ? "Deleting…" : "Delete"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

export function EntriesPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <EntriesPageContent />
    </Suspense>
  );
}
