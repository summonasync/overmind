"use client";

import { ErrorText } from "@/shared/ui/error-text";
import {
  useApprovalSettings,
  useApprovalSettingsMutation,
} from "@/features/projects/lib/use-approval-settings";

interface ProjectApprovalSettingsToggleProps {
  projectId: string;
}

export function ProjectApprovalSettingsToggle({
  projectId,
}: ProjectApprovalSettingsToggleProps) {
  const { data: settings, loading, error } = useApprovalSettings(projectId);
  const update = useApprovalSettingsMutation(projectId);

  if (loading) {
    return <p className="text-xs text-muted">Loading approval settings…</p>;
  }

  if (settings === null) {
    return <ErrorText error={error ?? new Error("Approval settings are unavailable.")} />;
  }

  return (
    <div className="space-y-2">
      <label className="flex items-start gap-3">
        <input
          type="checkbox"
          checked={settings.delegateApprovals}
          disabled={update.isPending}
          onChange={(event) => update.mutate(event.target.checked)}
          className="mt-1 h-4 w-4"
        />
        <span>
          <span className="block text-sm text-foreground">Delegate approvals to agent</span>
          <span className="block text-xs text-muted">
            When enabled, the embedded agent may execute validate and done actions on this
            project on the owner&apos;s explicit chat instruction. Every action is stamped with
            provenance in the logs.
          </span>
        </span>
      </label>
      <ErrorText error={update.error as Error | null} />
    </div>
  );
}
