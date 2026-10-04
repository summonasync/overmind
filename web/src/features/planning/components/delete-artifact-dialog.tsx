"use client";

import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { FeatureNumber } from "@/features/planning/components/feature-number";
import { artifactDeletePreview } from "@/features/planning/lib/artifact-deletion";
import type { PlanningArtifactResponse } from "@/features/planning/types";

interface DeleteArtifactDialogProps {
  artifact: PlanningArtifactResponse | null;
  siblings: PlanningArtifactResponse[];
  featureSlug: string;
  pending: boolean;
  error: Error | null;
  onClose: () => void;
  onConfirm: (artifact: PlanningArtifactResponse) => void;
}

export function DeleteArtifactDialog({
  artifact,
  siblings,
  featureSlug,
  pending,
  error,
  onClose,
  onConfirm,
}: DeleteArtifactDialogProps) {
  if (artifact === null) {
    return null;
  }

  const preview = artifactDeletePreview(artifact, siblings);

  return (
    <Modal title={`Delete ${preview.target}`} open onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-foreground">
          <FeatureNumber slug={featureSlug} />
          Delete {preview.target}? This cannot be undone.
        </p>
        <ul className="space-y-1.5 text-sm text-muted">
          {preview.consequences.map((line) => (
            <li key={line} className="flex gap-2">
              <span aria-hidden>·</span>
              <span>{line}</span>
            </li>
          ))}
        </ul>
        {preview.warnings.length > 0 ? (
          <ul className="space-y-1.5 text-sm text-warning" role="note">
            {preview.warnings.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        ) : null}
        <ErrorText error={error} />
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="danger" disabled={pending} onClick={() => onConfirm(artifact)}>
            {pending ? "Deleting…" : "Delete"}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
