"use client";

import { useState } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import type { PlanningArtifactKind } from "@/features/planning/types";

interface ArtifactDeclineDialogProps {
  kind: PlanningArtifactKind;
  pending: boolean;
  error: Error | null;
  onClose: () => void;
  onConfirm: (reason?: string) => void;
}

export function ArtifactDeclineDialog({
  kind,
  pending,
  error,
  onClose,
  onConfirm,
}: ArtifactDeclineDialogProps) {
  const [reason, setReason] = useState("");
  const trimmed = reason.trim();

  return (
    <div className="space-y-4">
      <p className="text-sm text-foreground">
        Decline the {kind}? It stays draft — the log gains a declined line the claiming agent reads on its next pass.
      </p>
      <Field label="Reason (optional)" htmlFor="artifact-decline-reason">
        <Input
          id="artifact-decline-reason"
          value={reason}
          maxLength={500}
          onChange={(event) => setReason(event.target.value)}
          placeholder="What fails validation?"
        />
      </Field>
      <ErrorText error={error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>
          Cancel
        </Button>
        <Button
          variant="danger"
          disabled={pending}
          onClick={() => onConfirm(trimmed.length > 0 ? trimmed : undefined)}
        >
          {pending ? "Declining…" : "Decline"}
        </Button>
      </div>
    </div>
  );
}
