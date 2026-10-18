"use client";

import { useState } from "react";
import { FolderInput } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { Modal } from "@/shared/ui/modal";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { FolderPickerModal } from "@/features/projects";
import { useInfoPanel } from "@/widgets/app-shell";
import { KitDetailPanel } from "@/widgets/kit-workspace/ui/kit-detail-panel";
import { KitForm, KitImportDialog, KitList, useKitImport, useKitList, useKitMutations } from "@/features/kits";
import type { KitFormValues, KitResponse } from "@/features/kits";
import { KitsInfoPanel } from "@/pages/kits-page/ui/kits-info-panel";

export function KitsPage() {
  const { data: kits, loading, error } = useKitList();
  const mutations = useKitMutations();
  const kitImport = useKitImport();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<KitResponse | null>(null);
  const [deleting, setDeleting] = useState<KitResponse | null>(null);
  const [expandedKitId, setExpandedKitId] = useState<string | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);

  useInfoPanel(<KitsInfoPanel />);

  const activeError =
    (mutations.create.error as Error | null) ??
    (mutations.update.error as Error | null) ??
    (mutations.remove.error as Error | null) ??
    error;

  const handleImportFromFolder = () => {
    setPickerOpen(true);
  };

  const handleFolderPicked = (path: string) => {
    setPickerOpen(false);
    setImportOpen(true);
    kitImport.scan.mutate(path);
  };

  const handleSubmit = (values: KitFormValues) => {
    if (editing) {
      mutations.update.mutate({ id: editing.id, values }, { onSuccess: () => setFormOpen(false) });
    } else {
      mutations.create.mutate(values, { onSuccess: () => setFormOpen(false) });
    }
  };

  const expandedKit = kits.find((kit) => kit.id === expandedKitId) ?? null;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Kits"
        description="Reusable coding-agent kits to standardize behavior, context, and workflows across projects."
        actions={
          <>
            <Button variant="accent" onClick={handleImportFromFolder}>
              <FolderInput size={14} aria-hidden />
              Import from folder
            </Button>
            <Button onClick={() => { setEditing(null); setFormOpen(true); }}>New kit</Button>
          </>
        }
      />

      <ErrorText error={activeError} />

      {loading ? (
        <LoadingState label="Loading kits…" />
      ) : (
        <>
          <KitList
            kits={kits}
            expandedKitId={expandedKitId}
            onToggle={(kit) => setExpandedKitId((current) => (current === kit.id ? null : kit.id))}
            onEdit={(kit) => { setEditing(kit); setFormOpen(true); }}
            onDelete={setDeleting}
          />
          {expandedKit ? <KitDetailPanel kit={expandedKit} /> : null}
        </>
      )}

      <Modal
        title={editing ? "Edit kit" : "New kit"}
        open={formOpen}
        onClose={() => setFormOpen(false)}
      >
        <KitForm
          initial={editing ?? undefined}
          pending={mutations.create.isPending || mutations.update.isPending}
          error={(mutations.create.error as Error | null) ?? (mutations.update.error as Error | null)}
          onSubmit={handleSubmit}
          onCancel={() => setFormOpen(false)}
        />
      </Modal>

      <Modal title="Delete kit" open={deleting !== null} onClose={() => setDeleting(null)}>        {deleting ? (
          <div className="space-y-4">
            <p className="text-sm text-foreground">
              Delete kit &quot;{deleting.name}&quot;? Active kits must be deactivated first.
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setDeleting(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                disabled={mutations.remove.isPending}
                onClick={() => mutations.remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })}
              >
                {mutations.remove.isPending ? "Deleting…" : "Delete"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>

      <KitImportDialog
        open={importOpen}
        onClose={() => setImportOpen(false)}
        kitImport={kitImport}
      />

      <FolderPickerModal
        title="Import from folder"
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={handleFolderPicked}
      />
    </div>
  );
}
