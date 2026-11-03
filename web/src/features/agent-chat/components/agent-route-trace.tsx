import type { AgentRouteTrace, AgentRoutingSource } from "@/features/agent-chat/types";

const SOURCE_HINTS: Record<Exclude<AgentRoutingSource, null>, string> = {
  structural: "structural rule",
  learned: "learned pattern",
  classification: "classifier",
};

interface AgentRouteTraceRowProps {
  traces: AgentRouteTrace[];
}

export function AgentRouteTraceRow({ traces }: AgentRouteTraceRowProps) {
  if (traces.length === 0) {
    return null;
  }
  return (
    <li className="px-2.5 py-1">
      <span className="flex flex-wrap items-center gap-1.5">
        {traces.map((trace, index) => (
          <RouteChip key={`${trace.target}-${trace.source}-${index}`} trace={trace} />
        ))}
      </span>
    </li>
  );
}

function RouteChip({ trace }: { trace: AgentRouteTrace }) {
  const visibleSource =
    trace.source === null || trace.source === "structural" ? null : trace.source;
  const hint =
    trace.source === null
      ? "Routed by the intent router"
      : `Routed by the intent router · ${SOURCE_HINTS[trace.source]}`;
  return (
    <span
      title={hint}
      className="inline-flex items-center rounded-full border border-border bg-muted/20 px-2 py-0.5 font-mono text-xs text-muted"
    >
      {visibleSource === null ? `→ ${trace.target}` : `→ ${trace.target} · ${visibleSource}`}
    </span>
  );
}
