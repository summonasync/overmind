import type { EmbeddingHealthLeg, VectorSearchHealth } from "@/shared/api/types";

export function embeddingLegLabel(leg: EmbeddingHealthLeg): string {
  return leg.model ? `Embedding (${leg.provider} · ${leg.model})` : `Embedding (${leg.provider})`;
}

export function failingLegSummaries(report: VectorSearchHealth): string[] {
  const summaries: string[] = [];

  if (!report.qdrant.up) {
    summaries.push(`Qdrant: ${report.qdrant.error ?? "unreachable"}`);
  }

  if (!report.embedding.up) {
    summaries.push(`${embeddingLegLabel(report.embedding)}: ${report.embedding.error ?? "unreachable"}`);
  }

  return summaries;
}
