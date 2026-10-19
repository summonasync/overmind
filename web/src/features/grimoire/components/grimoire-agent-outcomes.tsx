import { Badge } from "@/shared/ui/badge";
import { agentOutcomeTone } from "@/features/grimoire/lib/grimoire-view";
import type { GrimoireAgentOutcome } from "@/features/grimoire/types";

interface GrimoireAgentOutcomesProps {
  agents: GrimoireAgentOutcome[];
}

export function GrimoireAgentOutcomes({ agents }: GrimoireAgentOutcomesProps) {
  if (agents.length === 0) {
    return null;
  }

  return (
    <ul className="mt-3 space-y-1.5">
      {agents.map((outcome) => (
        <li key={outcome.agent} className="rounded-md border border-border bg-surface px-3 py-2 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium text-foreground">{outcome.agent}</span>
            <Badge tone={agentOutcomeTone(outcome)}>{outcome.outcome}</Badge>
          </div>
          {outcome.configPath ? (
            <p className="mt-0.5 truncate text-xs text-muted">{outcome.configPath}</p>
          ) : null}
          {outcome.error ? (
            <p className="mt-0.5 text-xs text-danger" role="alert">
              {outcome.error}
            </p>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
