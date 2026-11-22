"use client";

import { GRAPH_TOKENS, nodeColorName } from "@/features/journal/lib/graph-palette";
import { nodeKindLabel, stateLabel } from "@/features/journal/lib/graph-copy";
import { graphNodeStates, nodeKey } from "@/features/journal/lib/graph-payload";
import type { GraphField } from "@/features/journal/types";

interface GraphNodeListProps {
  field: GraphField;
  selectedKey: string | null;
  onSelect: (key: string | null) => void;
  onOpenNote: (id: string) => void;
}

// R7's path, not R7's decoration: the canvas is an augmentation and this list is how every node
// is reached without a pointer. All rows render, because a capped list would put some nodes'
// context panel out of the keyboard walk, and that is the one thing this surface may not do.
export function GraphNodeList({
  field,
  selectedKey,
  onSelect,
  onOpenNote,
}: GraphNodeListProps) {
  const states = graphNodeStates(field.nodes, field.edges);

  return (
    <section aria-label="Nodes in this view" className="space-y-1">
      <p className="text-xs text-muted">
        {field.nodes.length === 0
          ? "Nothing to list under these filters."
          : `${field.nodes.length} rows — the same nodes the canvas draws.`}
      </p>
      <ul className="max-h-[420px] space-y-1 overflow-y-auto pr-1">
        {field.nodes.map((node) => {
          const key = nodeKey(node.kind, node.id);
          const state = states.get(key) ?? "linked";
          const selected = key === selectedKey;

          return (
            <li key={key}>
              <div className="flex min-h-[32px] items-center gap-2">
                <button
                  type="button"
                  aria-pressed={selected}
                  onClick={() => onSelect(selected ? null : key)}
                  className="flex min-h-[32px] min-w-0 flex-1 items-center gap-2 rounded-md px-2 text-left text-sm text-foreground hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                >
                  <span
                    aria-hidden="true"
                    className="size-3 shrink-0 rounded-full border border-border"
                    style={{ backgroundColor: `var(${GRAPH_TOKENS[nodeColorName(node)]})` }}
                  />
                  <span className="min-w-0 flex-1 truncate">{node.label.trim() || "Untitled"}</span>
                  <span className="shrink-0 text-xs text-muted">{nodeKindLabel(node)}</span>
                  <span className="shrink-0 text-xs text-muted">{stateLabel(state)}</span>
                </button>
                {node.kind === "entry" ? (
                  <button
                    type="button"
                    onClick={() => onOpenNote(node.id)}
                    className="min-h-[32px] shrink-0 rounded-md px-2 text-xs text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                  >
                    Open
                  </button>
                ) : null}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
