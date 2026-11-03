"use client";

import { useAgentUsage } from "@/features/agent-chat/lib/use-agent-usage";
import {
  formatTokenCount,
  resolveBudgetMeterState,
  type AgentBudgetMeterState,
} from "@/features/agent-chat/lib/budget-meter-state";
import type { AgentUsage } from "@/features/agent-chat/types";
import { cn } from "@/shared/lib/cn";

const FILL_BY_STATE: Record<AgentBudgetMeterState, string> = {
  off: "",
  normal: "bg-primary/70",
  warn: "bg-warning",
  exhausted: "bg-danger",
};

interface AgentBudgetMeterProps {
  className?: string;
}

export function AgentBudgetMeter({ className }: AgentBudgetMeterProps) {
  const usage = useAgentUsage();
  const data = usage.data;

  return (
    <div className={cn("shrink-0 text-xs", className)}>
      {data === undefined ? null : <MeterBody usage={data} />}
    </div>
  );
}

function MeterBody({ usage }: { usage: AgentUsage }) {
  const state = resolveBudgetMeterState(usage);
  if (state === "off") {
    return (
      <span className="whitespace-nowrap text-muted" title="Daily token budget is disabled">
        Budget off
      </span>
    );
  }

  const exhausted = state === "exhausted";
  const percent = Math.min(100, Math.round((usage.spentToday / usage.dailyBudget) * 100));
  const tooltip = `${usage.spentToday.toLocaleString("en-US")} of ${usage.dailyBudget.toLocaleString("en-US")} tokens used today`;

  return (
    <span className="flex items-center gap-2" title={tooltip}>
      <span
        className="h-1 w-16 overflow-hidden rounded-full bg-muted/40"
        role="progressbar"
        aria-label="Daily token budget"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
      >
        <span
          className={cn("block h-full rounded-full", FILL_BY_STATE[state])}
          style={{ width: `${percent}%` }}
        />
      </span>
      <span className={cn("whitespace-nowrap tabular-nums", exhausted ? "text-danger" : "text-muted")}>
        {exhausted ? "Budget reached · " : ""}
        {formatTokenCount(usage.spentToday)} / {formatTokenCount(usage.dailyBudget)}
      </span>
    </span>
  );
}
