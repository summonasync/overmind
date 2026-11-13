import type {
  UsageFeatureSummaryRow,
  UsageSummaryResponse,
} from "@/features/usage/types";

export function featureUsageRow(
  summary: UsageSummaryResponse,
  slug: string,
): UsageFeatureSummaryRow | null {
  return summary.features.find((row) => row.slug === slug) ?? null;
}
