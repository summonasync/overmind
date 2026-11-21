"use client";

import { cn } from "@/shared/lib/cn";
import { formatBacklogAge, formatCalendarDate } from "@/shared/lib/relative-time";
import type { PlanningFeatureBoardItem } from "@/features/planning/types";

interface FeatureClaimChipProps {
  item: Pick<
    PlanningFeatureBoardItem,
    "claimedByAlias" | "claimedByVendor" | "claimedAt" | "claimState" | "claimedByHuman"
  >;
}

const chipBase = "inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium";

export function FeatureClaimChip({ item }: FeatureClaimChipProps) {
  if (item.claimedByAlias === null) {
    return null;
  }

  const holder = `${item.claimedByAlias} by ${item.claimedByVendor}`;
  const age = item.claimedAt === null ? null : formatBacklogAge(item.claimedAt);
  const claimedOn = item.claimedAt === null ? undefined : formatCalendarDate(item.claimedAt);

  if (item.claimedByHuman) {
    return (
      <>
        <span
          id="claim-chip-human-desc"
          role="note"
          className="sr-only"
        >{`Held by a human on the board (${holder})${claimedOn === undefined ? "" : ` — since ${claimedOn}`}`}</span>
        <span
          className={cn(chipBase, "border border-border/50 bg-surface-raised text-muted/80")}
          aria-describedby="claim-chip-human-desc"
          title={
            claimedOn === undefined
              ? `Held by a human on the board (${holder})`
              : `Held by a human on the board (${holder}) — since ${claimedOn}`
          }
        >
          human-held
          {age !== null ? ` · ${age}` : ""}
        </span>
      </>
    );
  }

  if (item.claimState === "stale") {
    return (
      <span
        className={cn(chipBase, "border border-border bg-surface-raised text-muted")}
        aria-describedby="claim-chip-stale-desc"
        title={claimedOn}
      >
        stale{age !== null ? ` ${age}` : ""} · {holder}
        <span id="claim-chip-stale-desc" className="sr-only">
          Claimed{claimedOn === undefined ? "" : ` since ${claimedOn}`} — the holder session is no
          longer alive, the claim can be taken over.
        </span>
      </span>
    );
  }

  if (item.claimState === "ended") {
    return (
      <span className={cn(chipBase, "border border-border/50 text-muted/70")}>
        {holder} · ended
      </span>
    );
  }

  return (
    <span
      className={cn(chipBase, "border border-success/30 bg-success/10 text-success")}
      aria-describedby="claim-chip-alive-desc"
      title={claimedOn}
      data-claim-state="alive"
    >
      <span aria-hidden className="inline-block h-1.5 w-1.5 rounded-full bg-success" />
      <span className="sr-only">alive</span>
      {holder}
      {age !== null ? ` · ${age}` : ""}
      <span id="claim-chip-alive-desc" className="sr-only">
        Claimed{claimedOn === undefined ? "" : ` since ${claimedOn}`} by a live session
      </span>
    </span>
  );
}
