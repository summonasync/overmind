import type { UsageTokenTotals } from "@/features/usage/types";

export function totalUsageTokens(totals: UsageTokenTotals): number {
  return (
    totals.inputTokens +
    totals.outputTokens +
    totals.cacheReadTokens +
    totals.cacheWriteTokens
  );
}

export function formatCompactTokens(tokens: number): string {
  if (tokens < 1_000) {
    return `${tokens}`;
  }
  if (tokens < 1_000_000) {
    return `${compactUnit(tokens / 1_000)}k`;
  }
  return `${compactUnit(tokens / 1_000_000)}M`;
}

export function formatCost(cost: number): string {
  return `$${(cost >= 1 ? cost.toFixed(2) : cost.toFixed(3))}`;
}

export function formatCostShare(cost: number, totalCost: number): string {
  if (totalCost <= 0) {
    return "0%";
  }
  return `${Math.round((cost / totalCost) * 100)}%`;
}

function compactUnit(value: number): string {
  const rounded = value.toFixed(1);
  return rounded.endsWith(".0") ? rounded.slice(0, -2) : rounded;
}
