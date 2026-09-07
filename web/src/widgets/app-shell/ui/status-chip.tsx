"use client";

import { useHealth } from "@/features/health";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import { cn } from "@/shared/lib/cn";

export function StatusChip() {
  const { data, vectorSearchDown, loading } = useHealth();

  const healthy = !loading && data !== null && !vectorSearchDown;
  const degraded = !loading && vectorSearchDown;
  const dotClass = loading ? "bg-muted" : healthy ? "bg-success" : "bg-danger";
  const label = loading ? "Checking…" : healthy ? "System Healthy" : degraded ? "Degraded" : "Unknown";

  return (
    <div
      className="flex items-center gap-2 rounded-full border border-border bg-surface px-3 py-1.5"
      role="status"
      title={degraded ? "Vector search is unavailable. Full-text search still works." : undefined}
    >
      <span className={cn("h-2 w-2 rounded-full", dotClass)} aria-hidden />
      <Zerling pose="smug" size={ZERLING_SIZES.chip} />
      <span className="text-xs font-medium text-foreground">{label}</span>
    </div>
  );
}
