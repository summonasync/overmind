"use client";

import { Megaphone, X } from "lucide-react";
import { useMarshalHint } from "@/widgets/marshal-hint/lib/use-marshal-hint";

export function MarshalHint() {
  const { dismissed, dismiss } = useMarshalHint();

  if (dismissed) {
    return null;
  }

  return (
    <aside
      role="note"
      className="flex items-start gap-2.5 rounded-lg border border-info/30 bg-info/10 px-3 py-2 text-xs text-info"
    >
      <Megaphone size={14} className="mt-0.5 shrink-0" aria-hidden />
      <p className="min-w-0 flex-1">
        Coaching several agents through one wave? Hand the lanes to a marshal — run{" "}
        <code className="rounded bg-surface-raised px-1 font-mono text-foreground">/wave-marshal</code>{" "}
        yourself or keep it fresh with{" "}
        <code className="rounded bg-surface-raised px-1 font-mono text-foreground">
          /loop 5m /wave-marshal
        </code>
        .
      </p>
      <button
        type="button"
        onClick={dismiss}
        aria-label="Dismiss the wave marshal hint"
        className="rounded p-0.5 text-info/70 transition-colors hover:text-info focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        <X size={14} aria-hidden />
      </button>
    </aside>
  );
}
