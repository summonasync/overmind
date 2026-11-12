export interface UsageFeatureSummaryRow {
  featureId: string;
  slug: string;
  records: number;
  inputTokens: number;
  outputTokens: number;
  cacheReadTokens: number;
  cacheWriteTokens: number;
  cost: number;
}

export interface UsageAgentSummaryRow {
  alias: string;
  records: number;
  inputTokens: number;
  outputTokens: number;
  cacheReadTokens: number;
  cacheWriteTokens: number;
  cost: number;
}

export interface UsageSummaryResponse {
  projectId: string;
  since: string | null;
  until: string | null;
  features: UsageFeatureSummaryRow[];
  agents: UsageAgentSummaryRow[];
}

export interface UsageTokenTotals {
  inputTokens: number;
  outputTokens: number;
  cacheReadTokens: number;
  cacheWriteTokens: number;
}
