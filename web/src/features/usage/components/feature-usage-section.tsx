"use client";

import { formatCompactTokens, formatCost, formatCostShare, totalUsageTokens } from "@/features/usage/lib/format";
import { featureUsageRow } from "@/features/usage/lib/usage-rows";
import { useUsageSummary } from "@/features/usage/lib/use-usage-summary";

interface FeatureUsageSectionProps {
  projectId: string;
  slug: string;
}

export function FeatureUsageSection({ projectId, slug }: FeatureUsageSectionProps) {
  const { data } = useUsageSummary(projectId);
  if (data === null) {
    return null;
  }
  const row = featureUsageRow(data, slug);
  const tokens = row !== null ? totalUsageTokens(row) : 0;
  if (row === null || tokens === 0) {
    return null;
  }

  const tokenLines: Array<[string, number]> = [
    ["Input", row.inputTokens],
    ["Output", row.outputTokens],
    ["Cache read", row.cacheReadTokens],
    ["Cache write", row.cacheWriteTokens],
  ];
  const projectCost = data.agents.reduce((sum, agent) => sum + agent.cost, 0);
  const agents = data.agents.filter((agent) => totalUsageTokens(agent) > 0);

  return (
    <section
      aria-label="Feature usage"
      className="space-y-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">Usage</h3>
        <span className="ml-auto font-mono text-xs text-muted">
          {row.records} record{row.records === 1 ? "" : "s"} · {formatCompactTokens(tokens)} tokens ·{" "}
          {formatCost(row.cost)}
        </span>
      </div>
      <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {tokenLines.map(([label, value]) => (
          <div key={label} className="rounded-md border border-border/50 bg-surface-raised px-2 py-1.5">
            <dt className="text-[10px] font-medium uppercase tracking-wide text-muted">{label}</dt>
            <dd className="font-mono text-xs text-foreground">{formatCompactTokens(value)}</dd>
          </div>
        ))}
      </dl>
      {agents.length > 0 ? (
        <div className="border-t border-border pt-3">
          <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">
            Agents · project share
          </h4>
          <ul className="space-y-1">
            {agents.map((agent) => (
              <li key={agent.alias} className="flex items-baseline gap-2 text-xs">
                <span className="truncate text-foreground">{agent.alias}</span>
                <span className="ml-auto shrink-0 font-mono text-muted">
                  {formatCost(agent.cost)} · {formatCostShare(agent.cost, projectCost)}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}
