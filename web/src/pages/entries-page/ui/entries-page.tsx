"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Modal } from "@/shared/ui/modal";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageBackLink } from "@/shared/ui/page-back-link";
import { PageHeader } from "@/shared/ui/page-header";
import { TabHeader } from "@/shared/ui/tab-header";
import { EntryEditor, EntryFilters, EntryList, EntryReadModal, TagManager, useEntryList, useEntryMutations } from "@/features/entries";
import type { EntryFormValues, EntryListFilters, EntryResponse } from "@/features/entries";
import { useInfoPanel } from "@/widgets/app-shell";
import { EntriesInfoPanel } from "@/pages/entries-page/ui/entries-info-panel";

function EntriesPageContent() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";

  const [filters, setFilters] = useState<EntryListFilters>({ type: "", tag: "" });
  const { data: entries, loading, error } = useEntryList(projectId, filters);
  const { create, update, remove } = useEntryMutations(projectId);

  useInfoPanel(projectId ? <EntriesInfoPanel entries={entries} loading={loading} /> : null);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<EntryResponse | null>(null);
  const [deleting, setDeleting] = useState<EntryResponse | null>(null);
  const [deleteReason, setDeleteReason] = useState("");
  const [reading, setReading] = useState<EntryResponse | null>(null);

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
      <PageBackLink href="/" label="All projects" />
      <TabHeader
        description="Capture knowledge, decisions and skills — turn experience into a lasting advantage."
        actions={<Button onClick={() => { setEditing(null); setFormOpen(true); }}>New entry</Button>}
      />

      <div className="flex flex-wrap items-center gap-2">
        <EntryFilters filters={filters} onChange={setFilters} />
        <TagManager />
      </div>
      <ErrorText error={activeError} />

      {loading ? (
        <LoadingState label="Loading entries…" />
      ) : (
        <EntryList
          entries={entries}
          onEdit={(entry) => { setEditing(entry); setFormOpen(true); }}
          onDelete={setDeleting}
          onRead={setReading}
        />
      )}

      <EntryReadModal
        entry={reading}
        onClose={() => setReading(null)}
        onEdit={(entry) => { setReading(null); setEditing(entry); setFormOpen(true); }}
      />

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

      <Modal
        title="Delete entry"
        open={deleting !== null}
        onClose={() => {
          setDeleting(null);
          setDeleteReason("");
        }}
      >
        {deleting ? (
          <div className="space-y-4">
            <p className="text-sm text-foreground">
              {deleting.type === "tombstone" ? (
                <>
                  Delete the tombstone &quot;{deleting.title}&quot;? This removes the grave for good — no
                  second tombstone is created.
                </>
              ) : (
                <>
                  Delete entry &quot;{deleting.title}&quot;? A tombstone entry will keep the memory of it —
                  title, date, provenance and final content. This cannot be undone.
                </>
              )}
            </p>
            {deleting.type !== "tombstone" ? (
              <Input
                value={deleteReason}
                onChange={(event) => setDeleteReason(event.target.value)}
                placeholder="Reason (optional) — recorded on the tombstone"
                maxLength={500}
              />
            ) : null}
            <div className="flex justify-end gap-2">
              <Button
                variant="secondary"
                onClick={() => {
                  setDeleting(null);
                  setDeleteReason("");
                }}
              >
                Cancel
              </Button>
              <Button
                variant="danger"
                disabled={remove.isPending}
                onClick={() =>
                  remove.mutate(
                    { id: deleting.id, reason: deleting.type === "tombstone" ? undefined : deleteReason },
                    { onSuccess: () => {
                      setDeleting(null);
                      setDeleteReason("");
                    } },
                  )
                }
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
