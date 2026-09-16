"use client";

import { useState } from "react";
import { RefreshCw, Unlink } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { formatImportedAt, KitOriginBadge, KitReimportReport, useKitImport } from "@/features/kits";
import type { KitReimportReportResponse, KitResponse } from "@/features/kits";

interface KitSourceSectionProps {
  kit: KitResponse;
}

export function KitSourceSection({ kit }: KitSourceSectionProps) {
  const { reimport, clearSource } = useKitImport();
  const [confirmingClear, setConfirmingClear] = useState(false);
  const [report, setReport] = useState<KitReimportReportResponse | null>(null);

  if (kit.sourceKind !== "imported") {
    return (
      <section>
        <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">Source</h3>
        <p className="flex items-center gap-2 text-xs text-muted">
          <KitOriginBadge kit={kit} />
          {kit.sourceKind === "bundled" ? "Ships with Overmind." : "Created manually — no import source."}
        </p>
        {kit.sourceKind === "bundled" ? (
          <p className="mt-1 text-xs text-muted">Edits fork this kit into your personal copy.</p>
        ) : null}
      </section>
    );
  }

  return (
    <section>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted">Source</h3>
      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-2 text-xs">
          <KitOriginBadge kit={kit} />
          <span className="min-w-0 break-all font-mono text-muted">{kit.sourcePath}</span>
          {kit.importedAtUtc ? (
            <span className="text-muted">imported {formatImportedAt(kit.importedAtUtc)}</span>
          ) : null}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            variant="accent"
            disabled={reimport.isPending}
            onClick={() => reimport.mutate(kit.id, { onSuccess: setReport })}
          >
            <RefreshCw size={13} aria-hidden />
            {reimport.isPending ? "Re-importing…" : "Re-import"}
          </Button>
          <Button variant="secondary" onClick={() => setConfirmingClear(true)}>
            <Unlink size={13} aria-hidden />
            Clear source
          </Button>
        </div>
        <ErrorText error={reimport.error ?? clearSource.error} />
      </div>

      <Modal title="Clear kit source" open={confirmingClear} onClose={() => setConfirmingClear(false)}>
        <div className="space-y-4">
          <p className="text-sm text-foreground">
            Clear the import source of &quot;{kit.name}&quot;? The kit and its entries stay, but the
            badge flips to manual and Re-import is disabled.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setConfirmingClear(false)}>
              Cancel
            </Button>
            <Button
              disabled={clearSource.isPending}
              onClick={() =>
                clearSource.mutate(kit.id, { onSuccess: () => setConfirmingClear(false) })
              }
            >
              {clearSource.isPending ? "Clearing…" : "Clear source"}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal title="Re-import report" open={report !== null} onClose={() => setReport(null)} size="xl">
        {report ? <KitReimportReport report={report} /> : null}
        <div className="mt-4 flex justify-end">
          <Button onClick={() => setReport(null)}>Close</Button>
        </div>
      </Modal>
    </section>
  );
}
