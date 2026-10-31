import { Badge } from "@/shared/ui/badge";
import { ErrorText } from "@/shared/ui/error-text";
import { cn } from "@/shared/lib/cn";
import { formatRelativeTime } from "@/shared/lib/relative-time";
import { agentsOf, ownerOf } from "@/features/wave-chat/lib/roster";
import { roleBadgeToneOf, roleTextClassOf } from "@/features/wave-chat/lib/role-color";
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
      className="flex flex-col gap-1.5 rounded-lg border border-border bg-surface p-2"
    >
      <h2 className="px-1.5 pt-1 text-xs font-semibold uppercase tracking-wide text-muted">
        In the wave
      </h2>
      {error ? <ErrorText error={error} /> : null}
      {loading ? (
        <p className="px-1.5 pb-1 text-xs text-muted">Loading roster…</p>
      ) : (
        <>
          {owner ? <OwnerRow participant={owner} /> : null}
          {agents.length === 0 ? (
            <p className="px-1.5 pb-1 text-xs text-muted">No agents on lanes right now</p>
          ) : (
            agents.map((agent) => (
              <AgentRow
                key={`${agent.lane ?? ""}:${agent.alias}:${agent.vendor ?? ""}`}
                agent={agent}
              />
            ))
          )}
        </>
      )}
    </section>
  );
}

function OwnerRow({ participant }: { participant: WaveParticipant }) {
  return (
    <div className="flex flex-col gap-1 rounded-md border border-border bg-surface-raised px-2 py-1.5">
      <span
        className={cn(
          "min-w-0 break-words font-mono text-xs font-semibold",
          roleTextClassOf(participant.role),
        )}
      >
        {participant.alias}
      </span>
      <div className="flex items-center gap-1">
        <Badge tone={roleBadgeToneOf(participant.role)}>{participant.role}</Badge>
      </div>
    </div>
  );
}

function AgentRow({ agent }: { agent: WaveParticipant }) {
  return (
    <div className="flex flex-col gap-1 rounded-md border border-border bg-surface-raised px-2 py-1.5">
      <div className="flex items-start justify-between gap-2">
        <span
          className={cn(
            "min-w-0 break-words font-mono text-xs font-semibold",
            roleTextClassOf(agent.role),
          )}
        >
          {agent.alias}
          {agent.vendor ? (
            <span className="font-sans text-xs font-normal text-muted"> by {agent.vendor}</span>
          ) : null}
        </span>
        {agent.lastSeenAt ? (
          <span className="shrink-0 text-xs text-muted">{formatRelativeTime(agent.lastSeenAt)}</span>
        ) : null}
      </div>
      <div className="flex items-center gap-1">
        <Badge tone={roleBadgeToneOf(agent.role)}>{agent.role}</Badge>
        {agent.lane ? <Badge tone="neutral">{agent.lane}</Badge> : null}
      </div>
    </div>
  );
}
