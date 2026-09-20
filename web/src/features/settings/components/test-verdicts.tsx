import { Badge } from "@/shared/ui/badge";
import type { EmbeddingHealthLeg, VectorSearchHealth } from "@/shared/api/types";

interface TestVerdictsProps {
  report: VectorSearchHealth;
}

export function TestVerdicts({ report }: TestVerdictsProps) {
  return (
    <div className="flex flex-col gap-1.5 text-xs">
      <VerdictRow
        label={`Qdrant (${report.qdrant.endpoint})`}
        up={report.qdrant.up}
        detail={null}
        error={report.qdrant.error}
      />
      <VerdictRow
        label={embeddingLabel(report.embedding)}
        up={report.embedding.up}
        detail={report.embedding.dimensions !== null ? `${report.embedding.dimensions} dims` : null}
        error={report.embedding.error}
      />
    </div>
  );
}

function embeddingLabel(leg: EmbeddingHealthLeg): string {
  return leg.model ? `Embedding (${leg.provider} · ${leg.model})` : `Embedding (${leg.provider})`;
}

interface VerdictRowProps {
  label: string;
  up: boolean;
  detail: string | null;
  error: string | null;
}

function VerdictRow({ label, up, detail, error }: VerdictRowProps) {
  return (
    <p className="flex flex-wrap items-center gap-2">
      <Badge tone={up ? "success" : "danger"}>{up ? "Up" : "Down"}</Badge>
      <span className="font-medium text-foreground">{label}</span>
      {up && detail !== null ? <span className="text-muted">{detail}</span> : null}
      {!up && error !== null ? <span className="text-danger">{error}</span> : null}
    </p>
  );
}
