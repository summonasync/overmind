"use client";

import { AlertTriangle, Check } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Input } from "@/shared/ui/input";
import { cn } from "@/shared/lib/cn";
import { SCAN_ISSUE_LABELS } from "@/features/kits/lib/import-labels";
import type { KitScanCandidateResponse } from "@/features/kits/types";

export interface CandidateDraft {
  selected: boolean;
  name: string;
}

interface KitImportCandidateRowProps {
  candidate: KitScanCandidateResponse;
  draft: CandidateDraft;
  duplicateName: boolean;
  existingNameCollision: boolean;
  onToggle: (selected: boolean) => void;
  onRename: (name: string) => void;
}

export function KitImportCandidateRow({
  candidate,
  draft,
  duplicateName,
  existingNameCollision,
  onToggle,
  onRename,
}: KitImportCandidateRowProps) {
  const alreadyImported = candidate.reImportMatchKitId !== null;
  const conflictedName = duplicateName || existingNameCollision;
  const entryCount =
    candidate.skills.length + candidate.gates.length + candidate.guards.length;

  return (
    <li
      className={cn(
        "space-y-2 rounded-md border border-border bg-surface px-3 py-2.5",
        alreadyImported && "opacity-60",
      )}
    >
      <div className="flex items-center gap-3">
        <input
          type="checkbox"
          checked={draft.selected}
          disabled={alreadyImported}
          onChange={(event) => onToggle(event.target.checked)}
          aria-label={`Import kit ${candidate.name}`}
          className="size-4 shrink-0 accent-primary"
        />
        <div className="min-w-0 flex-1">
          <Input
            value={draft.name}
            disabled={alreadyImported}
            onChange={(event) => onRename(event.target.value)}
            aria-label={`Kit name for ${candidate.name}`}
            className={cn("h-8 font-medium", conflictedName && draft.selected && "border-danger")}
          />
          {draft.selected && conflictedName ? (
            <p className="mt-1 flex items-center gap-1 text-xs text-danger">
              <AlertTriangle size={12} aria-hidden />
              {duplicateName
                ? "This name is used by another selected kit."
                : "A kit with this name already exists — rename to import as new."}
            </p>
          ) : null}
          {alreadyImported ? (
            <p className="mt-1 text-xs text-info">
              Already imported — use Re-import on the kit instead.
            </p>
          ) : null}
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <Badge>{entryCount} entries</Badge>
        </div>
      </div>

      <div className="flex flex-wrap gap-1.5 pl-7">
        {candidate.skills.map((skill) => (
          <span
            key={skill.relativePath}
            className="inline-flex items-center gap-1 rounded bg-type-skill/10 px-1.5 py-0.5 text-xs text-type-skill"
          >
            <Check size={10} aria-hidden />
            {skill.name}
            {skill.platform ? <span className="opacity-70">({skill.platform})</span> : null}
          </span>
        ))}
        {candidate.gates.length > 0 ? (
          <Badge tone="success">{candidate.gates.length} gates</Badge>
        ) : null}
        {candidate.guards.length > 0 ? (
          <Badge tone="danger">{candidate.guards.length} guards</Badge>
        ) : null}
      </div>

      {candidate.issues.length > 0 ? (
        <ul className="ml-7 space-y-1">
          {candidate.issues.map((issue) => (
            <li
              key={`${issue.code}-${issue.relativePath}`}
              className="flex items-center gap-2 rounded bg-warning/10 px-2 py-1 text-xs text-warning"
            >
              <AlertTriangle size={12} aria-hidden className="shrink-0" />
              <Badge tone="warning">{SCAN_ISSUE_LABELS[issue.code]}</Badge>
              <span className="min-w-0 truncate font-mono">{issue.relativePath}</span>
              <span className="min-w-0 flex-1 truncate">{issue.message}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </li>
  );
}
