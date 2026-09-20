import type { VectorSearchHealth } from "@/shared/api/types";

export interface HealthResponse {
  status: string;
  vectorSearch: VectorSearchHealth;
  version: string;
}
