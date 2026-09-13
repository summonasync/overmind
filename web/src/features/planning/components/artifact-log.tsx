import type { PlanningLogLine } from "@/features/planning/types";

interface ArtifactLogProps {
  log: PlanningLogLine[];
}

export function ArtifactLog({ log }: ArtifactLogProps) {
  if (log.length === 0) {
    return <p className="text-xs text-muted">No log entries yet.</p>;
  }

  return (
    <ol className="space-y-1.5">
      {log.map((line, index) => (
        <li key={index} className="flex gap-2 text-xs leading-relaxed">
          <span className="shrink-0 font-mono text-muted">{line.date ?? "—"}</span>
          <span className="min-w-0 text-foreground">{line.text}</span>
        </li>
      ))}
    </ol>
  );
}
