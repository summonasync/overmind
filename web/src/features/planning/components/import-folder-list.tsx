"use client";

import { ImportFolderRow } from "@/features/planning/components/import-folder-row";
import type { PlanningImportScanEntry } from "@/features/planning/types";

interface ImportFolderListProps {
  entries: PlanningImportScanEntry[];
  importingFolder: string | null;
  onImport: (entry: PlanningImportScanEntry) => void;
}

export function ImportFolderList({ entries, importingFolder, onImport }: ImportFolderListProps) {
  return (
    <ul className="space-y-1.5">
      {entries.map((entry) => (
        <ImportFolderRow
          key={entry.folder}
          entry={entry}
          importing={entry.folder === importingFolder}
          onImport={onImport}
        />
      ))}
    </ul>
  );
}
