"use client";

import {
  GRAPH_TOKENS,
  LEGEND_NODE_ROWS,
  nodeColorName,
} from "@/features/journal/lib/graph-palette";
import {
  isolatedInViewLabel,
  nodeCountLabel,
  orphanLabel,
} from "@/features/journal/lib/graph-copy";
import type { GraphField } from "@/features/journal/types";

interface GraphLegendProps {
  field: GraphField;
}

// The legend is the payload read back to the owner in words. Every count comes from the field the
// server answered — never from what the canvas happened to draw — and the off-view row is a NODE
// count in node wording, because the dropped-edge tally is not in the payload to speak about.
export function GraphLegend({ field }: GraphLegendProps) {
  const counts = field.counts;
  const perColor = new Map<string, number>();
  for (const node of field.nodes) {
    const color = nodeColorName(node);
    perColor.set(color, (perColor.get(color) ?? 0) + 1);
  }

  return (
    <section aria-label="Legend" className="space-y-2 rounded-md border border-border bg-surface p-3">
      <p className="text-sm text-foreground">{nodeCountLabel(counts)}</p>

      <ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted">
        {LEGEND_NODE_ROWS.map((row) => (
          <li key={row.color} className="flex min-h-[24px] items-center gap-2">
            <span
              aria-hidden="true"
              className="size-3 rounded-full border border-border"
              style={{ backgroundColor: `var(${GRAPH_TOKENS[row.color]})` }}
            />
            {row.label}
            <span className="text-foreground">{perColor.get(row.color) ?? 0}</span>
          </li>
        ))}
      </ul>

      <ul className="space-y-1 text-xs text-muted">
        <li className="flex min-h-[24px] items-center gap-2">
          <span
            aria-hidden="true"
            className="size-3 rounded-full border border-dashed"
            style={{ borderColor: `var(${GRAPH_TOKENS.muted})` }}
          />
          {orphanLabel(counts.orphans)} — drawn as a hollow shell.
        </li>
        <li className="flex min-h-[24px] items-center gap-2">
          <span
            aria-hidden="true"
            className="size-3 rounded-full border border-dashed"
            style={{
              borderColor: `var(${GRAPH_TOKENS.muted})`,
              backgroundColor: `var(${GRAPH_TOKENS.note})`,
            }}
          />
          {isolatedInViewLabel(counts.isolatedInView)} — painted, ringed, never dropped.
        </li>
        <li className="flex min-h-[24px] items-center gap-2">
          <span
            aria-hidden="true"
            className="size-3 rounded-full"
            style={{ backgroundColor: `var(${GRAPH_TOKENS.note})` }}
          />
          Linked nodes carry a solid line to what they link; a link to a node the filters hid
          leaves no line here.
        </li>
        <li className="flex min-h-[24px] items-center gap-2">
          <span
            aria-hidden="true"
            className="h-0.5 w-5"
            style={{ backgroundColor: `var(${GRAPH_TOKENS.edgeStrong})` }}
          />
          A line that strengthens under the cursor is incident to the hovered node; the rest of the
          field dims.
        </li>
      </ul>
    </section>
  );
}
