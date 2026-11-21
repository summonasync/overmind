"use client";

import { useState } from "react";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import {
  KitImportCandidateRow,
  type CandidateDraft,
} from "@/features/kits/components/kit-import-candidate-row";
import { useKitList } from "@/features/kits/lib/use-kits";
import type {
  KitImportSelectionRequest,
  KitScanCandidateResponse,
  KitScanPreviewResponse,
} from "@/features/kits/types";

function buildDrafts(candidates: KitScanCandidateResponse[]): Record<string, CandidateDraft> {
  const drafts: Record<string, CandidateDraft> = {};
  for (const candidate of candidates) {
    drafts[candidate.path] = {
      selected: candidate.reImportMatchKitId === null,
      name: candidate.name,
    };
  }
  return drafts;
}

interface KitImportWizardProps {
  preview: KitScanPreviewResponse;
  importing: boolean;
  error: Error | null;
  onConfirm: (selections: KitImportSelectionRequest[]) => void;
  onCancel: () => void;
}

export function KitImportWizard({ preview, importing, error, onConfirm, onCancel }: KitImportWizardProps) {
  const { data: kits } = useKitList();
  const [drafts, setDrafts] = useState<Record<string, CandidateDraft>>(() =>
    buildDrafts(preview.candidates),
  );

  const selected = preview.candidates.filter((candidate) => drafts[candidate.path]?.selected);

  const existingKitNames = new Set(kits.map((kit) => kit.name.trim().toLowerCase()));

  const nameCounts = new Map<string, number>();
  for (const candidate of selected) {
    const key = drafts[candidate.path].name.trim().toLowerCase();
    nameCounts.set(key, (nameCounts.get(key) ?? 0) + 1);
  }
  const hasDuplicateNames = [...nameCounts.values()].some((count) => count > 1);
  const hasBlankName = selected.some((candidate) => drafts[candidate.path].name.trim().length === 0);

  const handleConfirm = () => {
    onConfirm(
      selected.map((candidate) => ({
        relativePath: candidate.relativePath,
        name: drafts[candidate.path].name.trim(),
      })),
    );
  };

  if (preview.candidates.length === 0) {
    return (
      <EmptyState
        title="No importable kits found"
        hint="A kit candidate is a top-level folder with skill folders, gates/guards files, or prose at its own root — the scanned folder itself counts too."
        action={
          <Button variant="secondary" onClick={onCancel}>
            Close
          </Button>
        }
      />
    );
  }

  return (
    <div className="space-y-3">
      {preview.truncated ? (
        <p className="flex items-center gap-2 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-xs text-warning">
          <AlertTriangle size={14} aria-hidden />
          Scan was truncated — some folders were skipped.
        </p>
      ) : null}

      <ul className="space-y-2">
        {preview.candidates.map((candidate) => {
          const draft = drafts[candidate.path];
          const trimmed = draft.name.trim();
          const lowercaseName = trimmed.toLowerCase();
          const duplicateName = trimmed.length > 0 && (nameCounts.get(lowercaseName) ?? 0) > 1;
          const liveCollision = trimmed.length > 0 && existingKitNames.has(lowercaseName);
          const scanCollision = candidate.nameCollision && trimmed === candidate.name;
          return (
            <KitImportCandidateRow
              key={candidate.path}
              candidate={candidate}
              draft={draft}
              duplicateName={duplicateName}
              existingNameCollision={liveCollision || scanCollision}
              onToggle={(next) =>
                setDrafts((current) => ({
                  ...current,
                  [candidate.path]: { ...draft, selected: next },
                }))
              }
              onRename={(name) =>
                setDrafts((current) => ({
                  ...current,
                  [candidate.path]: { ...draft, name },
                }))
              }
            />
          );
        })}
      </ul>

      <ErrorText error={error} />

      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted">
          {selected.length} of {preview.candidates.length} kit(s) selected
        </span>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={importing || selected.length === 0 || hasBlankName || hasDuplicateNames}
          >
            {importing ? "Importing…" : `Import ${selected.length} kit(s)`}
          </Button>
        </div>
      </div>
    </div>
  );
}
