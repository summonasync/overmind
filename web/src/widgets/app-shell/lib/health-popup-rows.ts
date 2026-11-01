import type { HealthResponse } from "@/features/health/types";
import type { VectorSearchHealth } from "@/shared/api/types";

export interface HealthPopupInput {
  data: HealthResponse | null;
  vectorSearch: VectorSearchHealth | null;
  vectorSearchDown: boolean;
}

export interface HealthPopupRow {
  term: string;
  value: string;
}

export interface HealthPopupModel {
  rows: HealthPopupRow[];
  degraded: boolean;
}

export function healthPopupModel(health: HealthPopupInput): HealthPopupModel {
  const rows: HealthPopupRow[] = [];

  if (health.vectorSearch === null) {
    rows.push({ term: "Vector search", value: "Unknown" });
  } else {
    const qdrant = health.vectorSearch.qdrant;
    rows.push({
      term: "Qdrant",
      value: qdrant.up ? `Up · ${qdrant.endpoint}` : `Down · ${qdrant.error ?? "unreachable"}`,
    });

    const embedding = health.vectorSearch.embedding;
    const parts = [embedding.provider, embedding.model];
    if (embedding.dimensions !== null) {
      parts.push(`${embedding.dimensions}d`);
    }
    rows.push({
      term: "Embedding",
      value: embedding.up ? parts.filter((part) => part.length > 0).join(" · ") : `Down · ${embedding.error ?? "unreachable"}`,
    });
  }

  rows.push({ term: "Version", value: health.data?.version ?? "unknown" });

  return { rows, degraded: health.vectorSearchDown };
}
