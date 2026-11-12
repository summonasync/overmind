import { apiGet } from "@/shared/api/client";
import type { UsageSummaryResponse } from "@/features/usage/types";

export function getUsageSummary(
  projectId: string,
  since?: string,
  until?: string,
): Promise<UsageSummaryResponse> {
  const params = new URLSearchParams({ projectId });
  if (since !== undefined) {
    params.set("since", since);
  }
  if (until !== undefined) {
    params.set("until", until);
  }
  return apiGet<UsageSummaryResponse>(`/usage/summary?${params.toString()}`);
}
