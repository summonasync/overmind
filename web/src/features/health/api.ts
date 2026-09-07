import { apiGet } from "@/shared/api/client";
import type { HealthResponse } from "@/features/health/types";

export function fetchHealth(): Promise<HealthResponse> {
  return apiGet<HealthResponse>("/health");
}
