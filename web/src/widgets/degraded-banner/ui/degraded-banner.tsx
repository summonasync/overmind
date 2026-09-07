"use client";

import { useState } from "react";
import { X } from "lucide-react";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import { useHealth } from "@/features/health";

export function DegradedBanner() {
  const { vectorSearchDown, loading } = useHealth();
  const [dismissed, setDismissed] = useState(false);

  if (loading || !vectorSearchDown || dismissed) {
    return null;
  }

  return (
    <div className="border-b border-warning/40 bg-warning/10 px-4 py-2" role="status">
      <div className="flex items-center justify-center gap-2.5 text-sm text-warning">
        <Zerling pose="sleeping" size={ZERLING_SIZES.banner} />
        <span>Vector search unavailable — full-text only.</span>
        <button
          type="button"
          onClick={() => setDismissed(true)}
          aria-label="Dismiss degraded mode notice"
          className="rounded p-0.5 text-warning/70 transition-colors hover:text-warning focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <X size={14} aria-hidden />
        </button>
      </div>
    </div>
  );
}
