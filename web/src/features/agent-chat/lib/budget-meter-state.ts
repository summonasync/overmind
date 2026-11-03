import type { AgentUsage } from "@/features/agent-chat/types";

export type AgentBudgetMeterState = "off" | "normal" | "warn" | "exhausted";

const WARN_SPEND_RATIO = 0.8;

export function resolveBudgetMeterState(usage: AgentUsage): AgentBudgetMeterState {
  if (usage.dailyBudget <= 0) {
    return "off";
  }
  if (usage.spentToday >= usage.dailyBudget) {
    return "exhausted";
  }
  return usage.spentToday / usage.dailyBudget > WARN_SPEND_RATIO ? "warn" : "normal";
}

export function formatTokenCount(tokens: number): string {
  if (tokens < 1000) {
    return String(tokens);
  }
  const precision = tokens < 10_000 ? 1 : 0;
  return `${(tokens / 1000).toFixed(precision)}k`;
}
