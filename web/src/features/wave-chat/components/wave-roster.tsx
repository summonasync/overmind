import { Badge } from "@/shared/ui/badge";
import { ErrorText } from "@/shared/ui/error-text";
import { agentsOf, ownerOf } from "@/features/wave-chat/lib/roster";
import type { WaveParticipant } from "@/features/wave-chat/types";

interface WaveRosterProps {
  participants: WaveParticipant[];
  loading: boolean;
  error: Error | null;
}

export function WaveRoster({ participants, loading, error }: WaveRosterProps) {
  const owner = ownerOf(participants);
  const agents = agentsOf(participants);
  return (
    <section
      aria-label="Wave participants"
      className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-2"
    >
      <h2 className="px-1.5 pt-1 text-xs font-semibold uppercase tracking-wide text-muted">
        In the wave
      </h2>
      {error ? <ErrorText error={error} /> : null}
      {loading ? (
        <p className="px-1.5 pb-1 text-xs text-muted">Loading roster…</p>
      ) : (
        <>
          {owner ? <OwnerRow alias={owner.alias} /> : null}
          {agents.length === 0 ? (
            <p className="px-1.5 pb-1 text-xs text-muted">No agents on lanes right now</p>
          ) : (
            agents.map((agent) => (
              <AgentRow key={`${agent.lane ?? ""}:${agent.alias}`} agent={agent} />
            ))
          )}
        </>
      )}
    </section>
  );
}

function OwnerRow({ alias }: { alias: string }) {
  return (
    <div className="flex items-center justify-between gap-2 rounded-md px-1.5 py-1">
      <span className="truncate font-mono text-xs font-semibold text-foreground">{alias}</span>
      <Badge tone="primary">owner</Badge>
    </div>
  );
}

function AgentRow({ agent }: { agent: WaveParticipant }) {
  return (
    <div className="flex flex-col gap-0.5 rounded-md px-1.5 py-1">
      <div className="flex items-center justify-between gap-2">
        <span className="truncate font-mono text-xs font-semibold text-foreground">
          {agent.alias}
        </span>
        {agent.lane ? <Badge tone="neutral">{agent.lane}</Badge> : null}
      </div>
      {agent.vendor ? <span className="truncate text-xs text-muted">{agent.vendor}</span> : null}
    </div>
  );
}
