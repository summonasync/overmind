"use client";

import { useCallback, useReducer, useSyncExternalStore } from "react";
import {
  desktopPermission,
  requestDesktopPermission,
  useAttentionPrefs,
  desktopToggleState,
} from "@/widgets/app-shell";
import type { DesktopPermission } from "@/widgets/app-shell";
import { cn } from "@/shared/lib/cn";

const subscribePermission = () => () => undefined;

export function AttentionSettingsSection() {
  const { prefs, update } = useAttentionPrefs();
  const permission = useSyncExternalStore(
    subscribePermission,
    desktopPermission,
    () => "default" as DesktopPermission,
  );
  const [, bump] = useReducer((version: number) => version + 1, 0);

  const toggle = desktopToggleState(permission, prefs.desktop);

  const handleDesktopChange = useCallback(
    async (next: boolean) => {
      if (!next) {
        update({ ...prefs, desktop: false });
        return;
      }
      const granted = await requestDesktopPermission();
      update({ ...prefs, desktop: granted === "granted" });
      bump();
    },
    [prefs, update, bump],
  );

  return (
    <section className="rounded-lg border border-border bg-surface p-5">
      <h2 className="mb-1 text-base font-semibold text-foreground">Attention</h2>
      <p className="mb-4 text-xs text-muted">
        When the Overmind tab is hidden, approvals and questions reach you through an OS toast,
        a short chirp and tab attention (favicon plus title count). In focus only the in-app
        bubble shows.
      </p>
      <div className="space-y-3">
        <label className="flex items-start gap-3">
          <input
            type="checkbox"
            checked={toggle.checked}
            disabled={toggle.disabled}
            onChange={(event) => void handleDesktopChange(event.target.checked)}
            className="mt-1 h-4 w-4"
          />
          <span>
            <span className="block text-sm text-foreground">Desktop notifications</span>
            <span className={cn("block text-xs", toggle.warning ? "text-warning" : "text-muted")}>
              {toggle.hint}
            </span>
          </span>
        </label>
        <label className="flex items-start gap-3">
          <input
            type="checkbox"
            checked={prefs.chirp}
            onChange={(event) => update({ ...prefs, chirp: event.target.checked })}
            className="mt-1 h-4 w-4"
          />
          <span>
            <span className="block text-sm text-foreground">Attention sound</span>
            <span className="block text-xs text-muted">
              Short synthesized chirp while the tab is hidden.
            </span>
          </span>
        </label>
      </div>
    </section>
  );
}
