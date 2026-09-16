"use client";

import { Badge } from "@/shared/ui/badge";
import { cn } from "@/shared/lib/cn";
import { ENTRY_TYPE_META } from "@/shared/lib/entry-type";
import {
  REIMPORT_OUTCOME_LABELS,
  REIMPORT_OUTCOME_TONES,
} from "@/features/kits/lib/import-labels";
import type { KitReimportReportResponse } from "@/features/kits/types";

interface KitReimportReportProps {
  report: KitReimportReportResponse;
}

export function KitReimportReport({ report }: KitReimportReportProps) {
  return (
    <div className="space-y-3">
      <p className="text-xs text-muted">
        Re-imported from <span className="font-mono">{report.scannedPath}</span>
      </p>
      <div className="flex flex-wrap gap-1.5">
        <Badge tone="success">{report.addedCount} added</Badge>
        <Badge tone="info">{report.updatedCount} updated</Badge>
        <Badge>{report.keptLocalCount} kept local</Badge>
        <Badge tone="danger">{report.conflictedCount} conflicted</Badge>
        <Badge tone="warning">{report.removedCount} removed</Badge>
      </div>

      {report.entries.length === 0 ? (
        <p className="text-xs text-muted">Nothing changed since the last import.</p>
      ) : (
        <ul className="space-y-1">
          {report.entries.map((entry, index) => (
            <li
              key={`${entry.entryId ?? entry.title}-${index}`}
              className={cn(
                "flex items-center gap-2 rounded-md px-2 py-1 text-xs",
                entry.outcome === "conflicted"
                  ? "border border-danger/40 bg-danger/10"
                  : "bg-surface-raised",
              )}
            >
              <Badge tone={REIMPORT_OUTCOME_TONES[entry.outcome]}>
                {REIMPORT_OUTCOME_LABELS[entry.outcome]}
              </Badge>
              <Badge tone={ENTRY_TYPE_META[entry.entryType].tone}>{entry.entryType}</Badge>
              <span className="min-w-0 truncate font-semibold text-foreground">{entry.title}</span>
              {entry.relativePath ? (
                <span className="min-w-0 flex-1 truncate text-right font-mono text-muted">
                  {entry.relativePath}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
