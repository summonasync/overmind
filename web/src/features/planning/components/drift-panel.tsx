"use client";

import { useState } from "react";
import { RefreshCw } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { useMaterializeMutation, usePlanningDrift, usePlanningWorkflow } from "@/features/planning/lib/use-planning";
import type { PlanningDriftItem } from "@/features/planning/types";

interface DriftPanelProps {
  projectId: string;
}

export function DriftPanel({ projectId }: DriftPanelProps) {
  const { data: workflow } = usePlanningWorkflow(projectId);
  const materializationOn = workflow?.effectiveMaterialization === "on";
  const { data: drift, loading, error, refetch } = usePlanningDrift(materializationOn ? projectId : "");
  const { materialize } = useMaterializeMutation(projectId);
  const [confirmOpen, setConfirmOpen] = useState(false);

  if (!materializationOn) {
    return null;
  }

  return (
    <section className="space-y-3 rounded-lg border border-border bg-surface p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold text-foreground">Materialization drift</h2>
          <p className="text-xs text-muted">
            Files Overmind wrote into the project folder versus their current on-disk state.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="secondary" className="px-2.5 py-1 text-xs" disabled={loading} onClick={() => refetch()}>
            <RefreshCw size={12} aria-hidden />
            {loading ? "Checking…" : "Refresh"}
          </Button>
          <Button
            variant="secondary"
            className="px-2.5 py-1 text-xs"
            disabled={materialize.isPending}
            onClick={() => setConfirmOpen(true)}
          >
            Re-materialize
          </Button>
        </div>
      </div>

      <ErrorText error={error ?? (materialize.error as Error | null)} />

      {materialize.data !== undefined && materialize.data.warnings.length > 0 ? (
        <ul className="space-y-1">
          {materialize.data.warnings.map((warning) => (
            <li key={warning} className="text-xs text-warning">
              {warning}
            </li>
          ))}
        </ul>
      ) : null}

      {drift.length === 0 ? (
        <p className="text-xs text-success">Files match the database.</p>
      ) : (
        <ul className="space-y-1">
          {drift.map((item, index) => (
            <DriftRow key={`${item.relativePath}-${index}`} item={item} />
          ))}
        </ul>
      )}

      <Modal title="Re-materialize" open={confirmOpen} onClose={() => setConfirmOpen(false)}>
        <div className="space-y-4">
          <p className="text-sm text-foreground">
            Re-materialize rewrites every Overmind-managed file in the project folder from the
            database — local edits to those files are lost. Files not managed by Overmind are not
            touched.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setConfirmOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              disabled={materialize.isPending}
              onClick={() => materialize.mutate(true, { onSuccess: () => setConfirmOpen(false) })}
            >
              {materialize.isPending ? "Writing…" : "Overwrite files"}
            </Button>
          </div>
        </div>
      </Modal>
    </section>
  );
}

function DriftRow({ item }: { item: PlanningDriftItem }) {
  return (
    <li className="flex items-center gap-2 rounded-md bg-surface-raised px-2 py-1 text-xs">
      <Badge tone={item.kind === "missing" ? "warning" : "info"}>{item.kind}</Badge>
      <span className="min-w-0 truncate font-mono text-foreground">{item.relativePath}</span>
    </li>
  );
}
