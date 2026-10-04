"use client";

import { useState } from "react";
import { ChevronRight } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { featureStatusTone } from "@/features/planning/lib/status-tone";
import {
  importFolderState,
  importFolderStateLabel,
  importFolderStateTone,
} from "@/features/planning/lib/import-folder-state";
import type { PlanningImportScanEntry } from "@/features/planning/types";

interface ImportFolderRowProps {
  entry: PlanningImportScanEntry;
  importing: boolean;
  onImport: (entry: PlanningImportScanEntry) => void;
}

export function ImportFolderRow({ entry, importing, onImport }: ImportFolderRowProps) {
  const [open, setOpen] = useState(false);
  const state = importFolderState(entry);

  return (
    <li className="rounded-md bg-surface-raised">
      <div className="flex flex-wrap items-center gap-2 px-2 py-1.5">
        <button
          type="button"
          aria-expanded={open}
          aria-label={`${open ? "Hide" : "Show"} preview of ${entry.folder}`}
          onClick={() => setOpen((value) => !value)}
          className="rounded p-0.5 text-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <ChevronRight
            size={14}
            aria-hidden
            className={open ? "rotate-90 transition-transform" : "transition-transform"}
          />
        </button>
        <span className="min-w-0 truncate font-mono text-xs text-foreground">{entry.folder}</span>
        <Badge tone={importFolderStateTone(state)}>{importFolderStateLabel(state)}</Badge>
        {entry.title ? (
          <span className="hidden min-w-0 truncate text-xs text-muted sm:inline">{entry.title}</span>
        ) : null}
        <Button
          variant="secondary"
          className="ml-auto px-2.5 py-1 text-xs"
          disabled={state === "error" || importing}
          onClick={() => onImport(entry)}
        >
          {importing ? "Importing…" : "Import"}
        </Button>
      </div>
      {state === "error" ? (
        <p className="px-2 pb-1.5 text-xs text-danger" role="note">
          {entry.error}
        </p>
      ) : null}
      {open ? <ImportFolderPreview entry={entry} /> : null}
    </li>
  );
}

function ImportFolderPreview({ entry }: { entry: PlanningImportScanEntry }) {
  return (
    <dl className="space-y-1 border-t border-border/60 px-2 py-2 text-xs text-muted">
      <div className="flex gap-2">
        <dt className="w-20 shrink-0">Slug</dt>
        <dd className="min-w-0 truncate font-mono text-foreground">{entry.slug ?? "—"}</dd>
      </div>
      <div className="flex gap-2">
        <dt className="w-20 shrink-0">Title</dt>
        <dd className="min-w-0 truncate text-foreground">{entry.title ?? "—"}</dd>
      </div>
      <div className="flex items-center gap-2">
        <dt className="w-20 shrink-0">Status</dt>
        <dd>
          {entry.status ? (
            <Badge tone={featureStatusTone(entry.status)}>{entry.status}</Badge>
          ) : (
            "—"
          )}
        </dd>
      </div>
      <div className="flex gap-2">
        <dt className="w-20 shrink-0">Artifacts</dt>
        <dd>
          {entry.specs} specs · {entry.plans} plans · {entry.tasks} tasks · {entry.results} results
        </dd>
      </div>
    </dl>
  );
}
