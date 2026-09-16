"use client";

import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { Modal } from "@/shared/ui/modal";
import { KitImportReport } from "@/features/kits/components/kit-import-report";
import { KitImportWizard } from "@/features/kits/components/kit-import-wizard";
import type { useKitImport } from "@/features/kits/lib/use-kit-import";

interface KitImportDialogProps {
  open: boolean;
  onClose: () => void;
  kitImport: ReturnType<typeof useKitImport>;
}

export function KitImportDialog({ open, onClose, kitImport }: KitImportDialogProps) {
  const { scan, runImport } = kitImport;

  const handleClose = () => {
    scan.reset();
    runImport.reset();
    onClose();
  };

  return (
    <Modal title="Import kits from folder" open={open} onClose={handleClose} size="xl">
      {scan.isPending ? (
        <LoadingState label="Scanning folder…" />
      ) : scan.error ? (
        <div className="space-y-3">
          <ErrorText error={scan.error} />
          <div className="flex justify-end">
            <Button variant="secondary" onClick={handleClose}>
              Close
            </Button>
          </div>
        </div>
      ) : scan.data ? (
        runImport.data ? (
          <KitImportReport report={runImport.data} onDone={handleClose} />
        ) : (
          <KitImportWizard
            key={scan.data.scannedPath}
            preview={scan.data}
            importing={runImport.isPending}
            error={runImport.error}
            onConfirm={(kits) => runImport.mutate({ path: scan.data.scannedPath, kits })}
            onCancel={handleClose}
          />
        )
      ) : null}
    </Modal>
  );
}
