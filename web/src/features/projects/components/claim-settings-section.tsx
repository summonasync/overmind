"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Textarea } from "@/shared/ui/textarea";
import {
  useClaimSettings,
  useClaimSettingsMutation,
} from "@/features/projects/lib/use-claim-settings";
import type { ClaimSettingsResponse } from "@/features/projects/types";

interface ClaimSettingsSectionProps {
  projectId: string;
}

export function ClaimSettingsSection({ projectId }: ClaimSettingsSectionProps) {
  const { data: settings, loading, error } = useClaimSettings(projectId);
  const update = useClaimSettingsMutation(projectId);

  const [rules, setRules] = useState("");
  const [synced, setSynced] = useState<ClaimSettingsResponse | null>(null);

  if (settings !== null && synced !== settings) {
    setSynced(settings);
    setRules(settings.agentNamingRules ?? "");
  }

  if (loading) {
    return <p className="text-xs text-muted">Loading claim settings…</p>;
  }

  if (settings === null) {
    return <ErrorText error={error ?? new Error("Claim settings are unavailable.")} />;
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    update.mutate({
      claimStaleThresholdMinutes: settings.claimStaleThresholdMinutes,
      agentNamingRules: rules,
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <p className="text-xs text-muted">
        Naming rules agents read in the agentIdentity block of their onboard briefing — pin claim
        aliases and formatting expectations here. Empty — the global default applies.
      </p>
      <Textarea
        rows={6}
        aria-label="Agent naming rules"
        maxLength={2000}
        value={rules}
        onChange={(event) => setRules(event.target.value)}
        placeholder="Aliases look like Dan, Rex. One agent, one claim at a time…"
      />
      <p className="text-xs text-muted">{rules.length}/2000</p>
      <div className="flex items-center gap-3">
        <Button type="submit" disabled={update.isPending}>
          {update.isPending ? "Saving…" : "Save claim settings"}
        </Button>
        {update.isSuccess ? <span className="text-xs text-success">Saved.</span> : null}
      </div>
      <ErrorText error={update.error as Error | null} />
    </form>
  );
}
