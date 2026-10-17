"use client";

import { cn } from "@/shared/lib/cn";
import type { LaneClaimState } from "@/features/lanes/types";

interface LaneClaimChipProps {
  alias: string | null;
  vendor: string | null;
  claimState: LaneClaimState | null;
}

const chipBase = "inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium";

export function LaneClaimChip({ alias, vendor, claimState }: LaneClaimChipProps) {
  if (alias === null || vendor === null) {
    return null;
  }

  const holder = `${alias} by ${vendor}`;

  if (claimState === "stale") {
    return (
      <span className={cn(chipBase, "border border-border bg-surface-raised text-muted")}>
        stale · {holder}
      </span>
    );
  }

  if (claimState === "ended") {
    return (
      <span className={cn(chipBase, "border border-border/50 text-muted/70")}>
        {holder} · ended
      </span>
    );
  }

  return (
    <span
      className={cn(chipBase, "border border-success/30 bg-success/10 text-success")}
      data-claim-state="alive"
    >
      <span aria-hidden className="inline-block h-1.5 w-1.5 rounded-full bg-success" />
      <span className="sr-only">alive</span>
      {holder}
    </span>
  );
}
