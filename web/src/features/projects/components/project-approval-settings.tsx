"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Select } from "@/shared/ui/select";
import {
  useApprovalSettings,
  useApprovalSettingsMutation,
} from "@/features/projects/lib/use-approval-settings";
import {
  DELEGATE_APPROVALS_OVERRIDES,
  type ApprovalSettingsResponse,
  type DelegateApprovalsOverride,
} from "@/features/projects/types";

interface ProjectApprovalSettingsProps {
  projectId: string;
  globalDelegateApprovalsDefault?: "on" | "off";
}

const OVERRIDE_LABELS: Record<DelegateApprovalsOverride, string> = {
  unset: "Inherit",
  on: "On",
  off: "Off",
};

export function ProjectApprovalSettings({
  projectId,
  globalDelegateApprovalsDefault,
}: ProjectApprovalSettingsProps) {
  const { data: settings, loading, error } = useApprovalSettings(projectId);
  const update = useApprovalSettingsMutation(projectId);
  const [delegateApprovals, setDelegateApprovals] = useState<DelegateApprovalsOverride>("unset");
  const [synced, setSynced] = useState<ApprovalSettingsResponse | null>(null);

  if (settings !== null && synced !== settings) {
    setSynced(settings);
    setDelegateApprovals(settings.delegateApprovals);
  }

  if (loading) {
    return <p className="text-xs text-muted">Loading approval settings…</p>;
  }

  if (settings === null) {
    return <ErrorText error={error ?? new Error("Approval settings are unavailable.")} />;
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    update.mutate(delegateApprovals);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <p className="text-xs text-muted">
        When enabled, the embedded agent may execute validate and done actions on this project on
        the owner&apos;s explicit chat instruction. Inherit follows the global default in agent
        settings. Every action is stamped with provenance in the logs.
      </p>
      <Field label="Delegate approvals to agent" htmlFor="project-delegate-approvals">
        <Select
          id="project-delegate-approvals"
          value={delegateApprovals}
          onChange={(event) => setDelegateApprovals(event.target.value as DelegateApprovalsOverride)}
          className="w-full"
        >
          {DELEGATE_APPROVALS_OVERRIDES.map((option) => (
            <option key={option} value={option}>
              {OVERRIDE_LABELS[option]}
            </option>
          ))}
        </Select>
      </Field>
      <p className="text-sm font-medium text-foreground">
        {effectiveSummary(settings, globalDelegateApprovalsDefault)}
      </p>
      <div className="flex items-center gap-3">
        <Button type="submit" disabled={update.isPending}>
          {update.isPending ? "Saving…" : "Save approvals"}
        </Button>
        {update.isSuccess ? <span className="text-xs text-success">Saved.</span> : null}
      </div>
      <ErrorText error={update.error as Error | null} />
    </form>
  );
}

function effectiveSummary(
  settings: ApprovalSettingsResponse,
  globalDefault: "on" | "off" | undefined,
): string {
  const state = settings.effectiveDelegateApprovals ? "enabled" : "disabled";
  if (settings.delegateApprovals === "unset") {
    return `Currently: ${state} (inherited: global ${settings.effectiveDelegateApprovals ? "on" : "off"})`;
  }
  if (globalDefault !== undefined && (globalDefault === "on") === settings.effectiveDelegateApprovals) {
    return `Currently: ${state} (matches global default)`;
  }
  return `Currently: ${state} (project override)`;
}
