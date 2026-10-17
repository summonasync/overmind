"use client";

import { Badge } from "@/shared/ui/badge";
import { featureStatusTone } from "@/features/planning";
import { LaneClaimChip } from "@/features/lanes/components/lane-claim-chip";
import type { LaneFeatureSummary, LaneRow } from "@/features/lanes/types";

interface LaneCardProps {
  lane: LaneRow;
}

function LaneFeatureRow({ feature }: { feature: LaneFeatureSummary }) {
  return (
    <li className="flex items-center gap-2">
      <span className="min-w-0 truncate font-mono text-xs text-foreground">{feature.slug}</span>
      <Badge tone={featureStatusTone(feature.status)}>{feature.status}</Badge>
      <span className="ml-auto min-w-0 truncate">
        <LaneClaimChip
          alias={feature.claimedByAlias}
          vendor={feature.claimedByVendor}
          claimState={feature.claimState}
        />
      </span>
    </li>
  );
}

export function LaneCard({ lane }: LaneCardProps) {
  return (
    <li className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 transition-colors hover:border-border-strong">
      <div className="flex items-start justify-between gap-2">
        <p className="min-w-0 truncate text-sm font-semibold text-foreground">{lane.name}</p>
        <div className="flex shrink-0 items-center gap-1.5">
          {lane.contour !== null ? <Badge>{lane.contour}</Badge> : null}
          <Badge tone={lane.openFeatureCount > 0 ? "warning" : "neutral"}>
            {lane.openFeatureCount} open
          </Badge>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-xs text-muted">holder</span>
        {lane.holder === null ? (
          <span className="text-xs text-muted/70">unclaimed</span>
        ) : (
          <LaneClaimChip
            alias={lane.holder.alias}
            vendor={lane.holder.vendor}
            claimState={lane.holder.claimState}
          />
        )}
      </div>

      {lane.features.length === 0 ? (
        <p className="text-xs text-muted">No features in this lane yet.</p>
      ) : (
        <ul className="space-y-1.5 border-t border-border pt-2">
          {lane.features.map((feature) => (
            <LaneFeatureRow key={feature.slug} feature={feature} />
          ))}
        </ul>
      )}
    </li>
  );
}
