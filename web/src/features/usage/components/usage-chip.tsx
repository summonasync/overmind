"use client";

import { cn } from "@/shared/lib/cn";
import { formatCompactTokens, formatCost, totalUsageTokens } from "@/features/usage/lib/format";
import { featureUsageRow } from "@/features/usage/lib/usage-rows";
import { useUsageSummary } from "@/features/usage/lib/use-usage-summary";

interface UsageChipProps {
  projectId: string;
  slug: string;
}

export function UsageChip({ projectId, slug }: UsageChipProps) {
  const { data } = useUsageSummary(projectId);
  if (data === null) {
    return null;
  }
  const row = featureUsageRow(data, slug);
  const tokens = row !== null ? totalUsageTokens(row) : 0;
  if (row === null || tokens === 0) {
    return null;
  }

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium font-mono",
        "border border-border/50 bg-surface-raised text-muted",
      )}
      title={`${row.records} record${row.records === 1 ? "" : "s"}${
        row.cost > 0 ? ` · ${formatCost(row.cost)}` : ""
      }`}
    >
      {formatCompactTokens(tokens)}
      {row.cost > 0 ? ` · ${formatCost(row.cost)}` : ""}
    </span>
  );
}
