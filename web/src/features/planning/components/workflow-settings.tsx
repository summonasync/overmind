"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Select } from "@/shared/ui/select";
import { usePlanningWorkflow, useWorkflowMutations } from "@/features/planning/lib/use-planning";
import {
  HUMAN_VALIDATION_OVERRIDES,
  PLANNING_MATERIALIZATIONS,
  type HumanValidationOverride,
  type PlanningMaterialization,
  type PlanningWorkflowResponse,
} from "@/features/planning/types";

interface PlanningWorkflowSettingsProps {
  projectId: string;
}

export function PlanningWorkflowSettings({ projectId }: PlanningWorkflowSettingsProps) {
  const { data: workflow, loading, error } = usePlanningWorkflow(projectId);
  const { update } = useWorkflowMutations(projectId);

  const [materialization, setMaterialization] = useState<PlanningMaterialization>("inherit");
  const [requireHumanValidation, setRequireHumanValidation] = useState<HumanValidationOverride>("unset");
  const [synced, setSynced] = useState<PlanningWorkflowResponse | null>(null);

  if (workflow !== null && synced !== workflow) {
    setSynced(workflow);
    setMaterialization(workflow.materialization);
    setRequireHumanValidation(workflow.requireHumanValidation);
  }

  if (loading) {
    return <p className="text-xs text-muted">Loading planning workflow…</p>;
  }

  if (workflow === null) {
    return <ErrorText error={error ?? new Error("Planning workflow is unavailable.")} />;
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    update.mutate({ materialization, requireHumanValidation });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Materialization" htmlFor="workflow-materialization">
          <Select
            id="workflow-materialization"
            value={materialization}
            onChange={(event) => setMaterialization(event.target.value as PlanningMaterialization)}
            className="w-full"
          >
            {PLANNING_MATERIALIZATIONS.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Require human validation" htmlFor="workflow-validation">
          <Select
            id="workflow-validation"
            value={requireHumanValidation}
            onChange={(event) => setRequireHumanValidation(event.target.value as HumanValidationOverride)}
            className="w-full"
          >
            {HUMAN_VALIDATION_OVERRIDES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <p className="text-xs text-muted">
        Effective: {workflow.effectiveMaterialization}
        {workflow.materialization === "inherit" ? " — inherited from global default" : ""} · validation{" "}
        {workflow.effectiveRequireHumanValidation ? "on" : "off"}
        {workflow.requireHumanValidation === "unset" ? " — inherited from global default" : ""}
      </p>
      <div className="flex items-center gap-3">
        <Button type="submit" disabled={update.isPending}>
          {update.isPending ? "Saving…" : "Save workflow"}
        </Button>
        {update.isSuccess ? <span className="text-xs text-success">Saved.</span> : null}
      </div>
      <ErrorText error={update.error as Error | null} />
    </form>
  );
}
