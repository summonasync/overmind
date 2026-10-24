"use client";

import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import {
  featureResultTabHref,
  missingResultCopy,
  unvalidatedResultCopy,
  type DoneGateScenario,
} from "@/features/planning/lib/done-gate";

interface FeatureDoneDialogProps {
  scenario: DoneGateScenario;
  projectId: string;
  slug: string;
  title: string;
  refusal: string | null;
  pending: boolean;
  error: Error | null;
  onApprove: () => void;
  onCancel: () => void;
}

export function FeatureDoneDialog({
  scenario,
  projectId,
  slug,
  title,
  refusal,
  pending,
  error,
  onApprove,
  onCancel,
}: FeatureDoneDialogProps) {
  return (
    <div className="space-y-4">
      {scenario === "missing-result" ? (
        <>
          <p className="text-sm text-foreground">{missingResultCopy(title)}</p>
          <a
            href={featureResultTabHref(projectId, slug)}
            className="text-sm text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            Open the result tab →
          </a>
        </>
      ) : null}
      {scenario === "unvalidated-result" ? (
        <p className="text-sm text-foreground">{unvalidatedResultCopy(title)}</p>
      ) : null}
      {scenario === "refused" && refusal !== null ? (
        <ErrorText error={new Error(refusal)} />
      ) : null}
      <ErrorText error={scenario === "refused" ? null : error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        {scenario === "unvalidated-result" ? (
          <Button variant="primary" disabled={pending} onClick={onApprove}>
            {pending ? "Approving & done…" : "Approve & done"}
          </Button>
        ) : null}
      </div>
    </div>
  );
}
