"use client";

import { Suspense, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageBackLink } from "@/shared/ui/page-back-link";
import { PageHeader } from "@/shared/ui/page-header";
import { emptyGraphField } from "@/features/journal/lib/graph-payload";
import { emptyFieldMessage } from "@/features/journal/lib/graph-copy";
import { withFocus } from "@/features/journal/lib/graph-filters";
import { GraphCanvas } from "@/features/journal/components/graph-canvas";
import { GraphFilters } from "@/features/journal/components/graph-filters";
import { GraphLegend } from "@/features/journal/components/graph-legend";
import { GraphNodeList } from "@/features/journal/components/graph-node-list";
import { GraphNodePanel } from "@/features/journal/components/graph-node-panel";
import {
  useGraphField,
  useGraphFilters,
  useReducedMotion,
} from "@/features/journal/lib/use-graph";
import { noteHrefFromGraph } from "@/features/journal/lib/note-rails";
import type { GraphField } from "@/features/journal/types";

// The route is the app's first heavy client island under a static export: the shell below is a
// client component by necessity (the filters are the URL and the canvas needs a DOM), and the
// page file that mounts it is a server component so the island stays out of every other route's
// bundle. R4's click rule lives here, not in the canvas: selection is state, never a route.
function GraphBranch() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { filters, setFilter, clear } = useGraphFilters();
  const { field, loading, error, refetch } = useGraphField(filters);
  const reducedMotion = useReducedMotion();
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const shown: GraphField = field ?? emptyGraphField();
  const selectedNode = useMemo(
    () => shown.nodes.find((node) => `${node.kind}:${node.id}` === selectedKey) ?? null,
    [selectedKey, shown.nodes],
  );

  // A note opened from the field carries the field's filters along, so the note view's back
  // link brings the owner home to the view they left rather than to the inbox.
  const openNote = (id: string) =>
    router.push(noteHrefFromGraph(id, searchParams?.toString() ?? ""));

  if (loading && field === undefined) {
    return <LoadingState label="Loading the field…" />;
  }
  if (error !== null) {
    return (
      <div className="space-y-2">
        <ErrorText error={error} />
        <button
          type="button"
          onClick={() => refetch()}
          className="min-h-[32px] rounded-md px-2 text-sm text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          Retry
        </button>
      </div>
    );
  }

  const empty = shown.nodes.length === 0;

  return (
    <div className="space-y-4">
      <GraphFilters filters={filters} onChange={setFilter} onClear={clear} />

      <div className="flex flex-wrap items-start gap-3">
        <div className="min-w-[420px] flex-1 space-y-3">
          <GraphCanvas
            field={shown}
            selectedKey={selectedKey}
            reducedMotion={reducedMotion}
            onSelect={setSelectedKey}
            onOpenNote={openNote}
          />
          <GraphLegend field={shown} />
        </div>

        <div className="w-[320px] shrink-0 space-y-3">
          <GraphNodePanel
            field={shown}
            node={selectedNode}
            focusedHandle={filters.focus}
            onClear={() => setFilter(withFocus(filters, ""))}
            onCentre={(handle) => setFilter(withFocus(filters, handle))}
            onOpenNote={openNote}
          />
          {empty ? (
            <p className="text-sm text-muted" role="status">
              {emptyFieldMessage(filters.search.trim().length > 0, filters.search)}
            </p>
          ) : null}
          <GraphNodeList
            field={shown}
            selectedKey={selectedKey}
            onSelect={setSelectedKey}
            onOpenNote={openNote}
          />
        </div>
      </div>
    </div>
  );
}

export function JournalGraphPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <div className="space-y-4">
        <PageBackLink href="/journal" label="Journal inbox" />
        <PageHeader
          title="Journal graph"
          description="Every note, project and feature the links reach, drawn from the same field the filters define."
        />
        <GraphBranch />
      </div>
    </Suspense>
  );
}
