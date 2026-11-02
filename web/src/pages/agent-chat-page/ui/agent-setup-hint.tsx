"use client";

import { useSettings } from "@/features/settings";

export function AgentSetupHint() {
  const { data: settings, loading } = useSettings();

  if (loading || !settings || settings.agentApiKeyConfigured) {
    return null;
  }

  return (
    <div
      className="mb-4 flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-warning"
      role="status"
    >
      <span>
        No LLM provider configured — messages are stored, but the agent cannot reply yet.
      </span>
      <a href="/settings" className="font-medium underline underline-offset-2 hover:text-warning/80">
        Open settings
      </a>
    </div>
  );
}
