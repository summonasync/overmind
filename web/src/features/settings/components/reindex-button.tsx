"use client";

import { Button } from "@/shared/ui/button";
import type { ReindexReport } from "@/features/settings/types";

interface ReindexButtonProps {
  pending: boolean;
  report: ReindexReport | null;
  onReindex: () => void;
}

export function ReindexButton({ pending, report, onReindex }: ReindexButtonProps) {
  return (
    <div className="space-y-2">
      <Button variant="secondary" onClick={onReindex} disabled={pending}>
        {pending ? "Reindexing…" : "Reindex all entries"}
      </Button>
      {report ? (
        <p className="text-xs text-muted">
          Reindexed {report.indexed} of {report.total} entries
          {report.failed > 0 ? `, ${report.failed} failed` : ""}.
        </p>
      ) : null}
    </div>
  );
}
