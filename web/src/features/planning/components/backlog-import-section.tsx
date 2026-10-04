"use client";

import { useState } from "react";
import { RefreshCw } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { ImportFolderList } from "@/features/planning/components/import-folder-list";
import { importConfirmText, importReportLine } from "@/features/planning/lib/import-folder-state";
import {
  usePlanningFolderImport,
  usePlanningImportScan,
} from "@/features/planning/lib/use-planning";
import type {
  PlanningFeatureImportReport,
  PlanningImportScanEntry,
} from "@/features/planning/types";

interface BacklogImportSectionProps {
  projectId: string;
}

export function BacklogImportSection({ projectId }: BacklogImportSectionProps) {
  const scan = usePlanningImportScan(projectId);
  const folderImport = usePlanningFolderImport(projectId);
  const [confirmEntry, setConfirmEntry] = useState<PlanningImportScanEntry | null>(null);
  const [reports, setReports] = useState<PlanningFeatureImportReport[]>([]);
  const [importedFolders, setImportedFolders] = useState<ReadonlySet<string>>(new Set());

  const report = scan.report;
  const entries = report?.entries ?? [];

  const runImport = (entry: PlanningImportScanEntry) => {
    folderImport.importFolder(entry.folder, (imported) => {
      setConfirmEntry(null);
      setReports((previous) => [...previous, imported]);
      setImportedFolders((previous) => new Set(previous).add(entry.folder));
      scan.scan();
    });
  };

  const handleImportClick = (entry: PlanningImportScanEntry) => {
    if (importConfirmText(entry, importedFolders) !== null) {
      setConfirmEntry(entry);
      return;
    }
    runImport(entry);
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <p className="text-xs text-muted">
          Scan the project BACKLOG folder and import feature folders onto the planning board.
        </p>
        <Button
          variant="secondary"
          className="px-2.5 py-1 text-xs"
          disabled={scan.scanning}
          onClick={() => {
            setConfirmEntry(null);
            scan.scan();
          }}
        >
          <RefreshCw size={12} aria-hidden />
          {scan.scanning ? "Scanning…" : "Scan BACKLOG"}
        </Button>
      </div>

      <ErrorText error={scan.error ?? folderImport.error} />

      {confirmEntry ? (
        <div className="space-y-2 rounded-md border border-warning/40 bg-warning/10 px-3 py-2">
          <p className="text-sm text-foreground">{importConfirmText(confirmEntry)}</p>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" className="px-2.5 py-1 text-xs" onClick={() => setConfirmEntry(null)}>
              Cancel
            </Button>
            <Button
              className="px-2.5 py-1 text-xs"
              disabled={folderImport.pending}
              onClick={() => runImport(confirmEntry)}
            >
              {folderImport.pending ? "Importing…" : "Import again"}
            </Button>
          </div>
        </div>
      ) : null}

      {report ? (
        entries.length === 0 ? (
          <p className="text-xs text-muted">No feature folders found.</p>
        ) : (
          <div className="space-y-2">
            <p className="truncate font-mono text-xs text-muted" title={report.root}>
              {report.root}
            </p>
            <ImportFolderList
              entries={entries}
              importingFolder={folderImport.importingFolder}
              onImport={handleImportClick}
            />
          </div>
        )
      ) : null}

      {reports.length > 0 ? (
        <ul className="space-y-1" role="status">
          {reports.map((item, index) => (
            <li key={`${item.slug}-${index}`} className="text-xs text-success">
              {importReportLine(item)}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
