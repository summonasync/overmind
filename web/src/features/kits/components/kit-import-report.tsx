"use client";

import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { IMPORT_FAILURE_LABELS } from "@/features/kits/lib/import-labels";
import type { KitImportReportResponse } from "@/features/kits/types";

interface KitImportReportProps {
  report: KitImportReportResponse;
  onDone: () => void;
}

export function KitImportReport({ report, onDone }: KitImportReportProps) {
  const imported = report.kits.filter((kit) => kit.success).length;

  return (
    <div className="space-y-3">
      <p className="text-xs text-muted">
        Imported {imported} of {report.kits.length} kit(s) from{" "}
        <span className="font-mono">{report.scannedPath}</span>
      </p>
      <ul className="space-y-1">
        {report.kits.map((kit) => (
          <li
            key={`${kit.name}-${kit.kitId ?? "failed"}`}
            className="flex items-center gap-2 rounded-md bg-surface px-2 py-1.5 text-xs"
          >
            <Badge tone={kit.success ? "success" : "danger"}>
              {kit.success
                ? "imported"
                : kit.failureKind
                  ? IMPORT_FAILURE_LABELS[kit.failureKind]
                  : "Failed"}
            </Badge>
            <span className="font-semibold text-foreground">{kit.name}</span>
            {kit.error ? <span className="min-w-0 flex-1 truncate text-danger">{kit.error}</span> : null}
          </li>
        ))}
      </ul>
      <div className="flex justify-end">
        <Button onClick={onDone}>Done</Button>
      </div>
    </div>
  );
}
