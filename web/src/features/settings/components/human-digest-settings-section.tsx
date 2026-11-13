"use client";

import { useSettingsMutations } from "@/features/settings/lib/use-settings";
import type { SettingsResponse } from "@/features/settings/types";
import { ErrorText } from "@/shared/ui/error-text";

interface HumanDigestSettingsSectionProps {
  settings: SettingsResponse;
}

export function HumanDigestSettingsSection({ settings }: HumanDigestSettingsSectionProps) {
  const { digestSave } = useSettingsMutations();

  return (
    <section id="human-digest" className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
      <h2 className="mb-1 text-base font-semibold text-foreground">Human digest</h2>
      <p className="mb-4 text-xs text-muted">
        Daily sweep that bundles the human-inbox backlog of a project into one notification
        instead of leaving replies to pile up unnoticed.
      </p>
      <label className="flex items-start gap-3">
        <input
          type="checkbox"
          checked={settings.humanDigestEnabled}
          disabled={digestSave.isPending}
          onChange={(event) => digestSave.mutate({ humanDigestEnabled: event.target.checked })}
          className="mt-1 h-4 w-4"
        />
        <span>
          <span className="block text-sm text-foreground">Digest sweep enabled</span>
          <span className="block text-xs text-muted">
            Per-project switches decide which projects opt in — a project is swept only when
            this switch and the project switch are both on.
          </span>
        </span>
      </label>
      <ErrorText error={digestSave.error as Error | null} />
    </section>
  );
}
