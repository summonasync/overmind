"use client";

import { useHealth } from "@/features/health";
import { healthPopupModel } from "@/widgets/app-shell/lib/health-popup-rows";

export function HealthPopupContent() {
  const { data, vectorSearch, vectorSearchDown } = useHealth();
  const model = healthPopupModel({ data, vectorSearch, vectorSearchDown });

  return (
    <span className="flex flex-col gap-1.5">
      {model.rows.map((row) => (
        <span key={row.term} className="flex items-baseline justify-between gap-3">
          <span className="shrink-0 text-muted">{row.term}</span>
          <span className="break-all text-right">{row.value}</span>
        </span>
      ))}
      {model.degraded ? (
        <a
          href="/settings"
          className="font-medium text-warning underline underline-offset-2 transition-colors hover:text-warning/80"
        >
          Open settings
        </a>
      ) : null}
    </span>
  );
}
