"use client";

import { Button } from "@/shared/ui/button";
import { LoadingState } from "@/shared/ui/loading-state";
import { ErrorText } from "@/shared/ui/error-text";
import { MarkdownViewer } from "@/shared/ui/markdown-viewer";
import {
  GRAPH_CONTROL_LABELS,
  nodeKindLabel,
  noteExcerpt,
  stateLabel,
} from "@/features/journal/lib/graph-copy";
import { focusHandleOf } from "@/features/journal/lib/graph-filters";
import { graphNodeStates, nodeKey } from "@/features/journal/lib/graph-payload";
import { useJournalNote } from "@/features/journal/lib/use-journal";
import { noteHref } from "@/features/journal/lib/note-rails";
import { PromoteNote } from "@/features/journal/components/promote-note";
import type { GraphField, GraphNode, GraphViewFilters } from "@/features/journal/types";

interface GraphNodePanelProps {
  field: GraphField;
  node: GraphNode | null;
  focusedHandle: string;
  // The panel needs the filters for one thing only: handing them to the promotion, so a successful
  // act stales the field the owner is looking at rather than every field the cache has held.
  filters?: GraphViewFilters;
  onClear: () => void;
  onCentre: (handle: string) => void;
  onOpenNote: (id: string) => void;
}

// R4's click target: the panel answers the node, and the URL stays where it was. Only an
// explicit "centre" act writes a filter, which is what keeps `focus` honest as a filter rather
// than as a echo of every cursor position.
export function GraphNodePanel({
  field,
  node,
  focusedHandle,
  filters,
  onClear,
  onCentre,
  onOpenNote,
}: GraphNodePanelProps) {
  if (node === null) {
    return (
      <aside aria-label="Node context" className="space-y-2 rounded-md border border-border bg-surface p-3">
        <p className="text-sm text-muted">
          Nothing selected. Pick a node on the field or in the list to read its context here.
        </p>
        {focusedHandle.length > 0 ? (
          <Button size="sm" variant="secondary" onClick={onClear}>
            {GRAPH_CONTROL_LABELS.clearCentre}
          </Button>
        ) : null}
      </aside>
    );
  }

  const key = nodeKey(node.kind, node.id);
  const state = graphNodeStates(field.nodes, field.edges).get(key) ?? "linked";
  const incident = field.edges.filter(
    (edge) =>
      nodeKey(edge.fromKind, edge.fromId) === key || nodeKey(edge.toKind, edge.toId) === key,
  ).length;
  const handle = focusHandleOf(node.kind, node.id);

  return (
    <aside aria-label="Node context" className="space-y-3 rounded-md border border-border bg-surface p-3">
      <div className="space-y-1">
        <h2 className="text-sm font-medium text-foreground">{node.label.trim() || "Untitled"}</h2>
        <p className="text-xs text-muted">
          {nodeKindLabel(node)} · {stateLabel(state)} · {incident}{incident === 1 ? " link" : " links"} in this view
          {node.featureStatus !== null ? ` · ${node.featureStatus}` : ""}
        </p>
      </div>

      {node.kind === "entry" ? (
        <NoteContext id={node.id} filters={filters} onOpenNote={onOpenNote} />
      ) : (
        <p className="text-xs text-muted">{addressGap(node.kind)}</p>
      )}

      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="secondary" onClick={() => onCentre(handle)}>
          {GRAPH_CONTROL_LABELS.centre}
        </Button>
        {focusedHandle.length > 0 ? (
          <Button size="sm" variant="ghost" onClick={onClear}>
            {GRAPH_CONTROL_LABELS.clearCentre}
          </Button>
        ) : null}
      </div>
    </aside>
  );
}

// The note's own context comes from the same detail door the note view uses — preview plus
// backlinks — because a second "graph context" endpoint would be a second definition of what a
// note is, and R2 says the graph does not invent link semantics.
function NoteContext({
  id,
  filters,
  onOpenNote,
}: {
  id: string;
  filters?: GraphViewFilters;
  onOpenNote: (id: string) => void;
}) {
  const note = useJournalNote(id);

  if (note.isPending) {
    return <LoadingState label="Loading the note…" />;
  }
  if (note.error !== null) {
    return <ErrorText error={note.error} />;
  }

  const data = note.data;
  if (data === undefined) {
    return null;
  }

  return (
    <div className="space-y-2">
      {/* The preview box carries no fill of its own: a raised surface would drop the markdown
          links the viewer paints in --color-primary to 4.3605:1, under the AA text floor this
          card is held to. The border is the separation. */}
      <div className="max-h-[220px] overflow-y-auto rounded-md border border-border p-2 text-sm text-foreground">
        <MarkdownViewer content={noteExcerpt(data.content)} />
      </div>
      <p className="text-xs text-muted">
        {data.incoming.length === 0
          ? "No note links here yet."
          : `${data.incoming.length} ${data.incoming.length === 1 ? "note links" : "notes link"} here.`}
      </p>
      {data.incoming.length > 0 ? (
        <ul className="space-y-1">
          {data.incoming.slice(0, 4).map((backlink) => (
            <li key={backlink.entryId} className="min-w-0">
              <a
                href={noteHref(backlink.entryId)}
                onClick={(event) => {
                  event.preventDefault();
                  onOpenNote(backlink.entryId);
                }}
                className="block truncate text-sm text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                {backlink.title}
              </a>
            </li>
          ))}
        </ul>
      ) : null}
      <Button size="sm" variant="accent" onClick={() => onOpenNote(id)}>
        {GRAPH_CONTROL_LABELS.openNote}
      </Button>
      {/* R6's exit door, reachable from the node the owner is already reading. The panel keeps the
          field's filters so the promotion stales the field this surface is drawing, not every
          field the cache has ever held. */}
      <PromoteNote entryId={id} noteTitle={data.title} graphFilters={filters} />
    </div>
  );
}

// Same posture as the outgoing rail: a project or feature is addressed by more than its guid,
// and the payload does not carry the rest, so the panel states the gap instead of guessing a URL
// that would land the owner on the wrong board.
function addressGap(kind: GraphNode["kind"]): string {
  return kind === "project"
    ? "Projects open from the dashboard by name; this node carries the guid the graph stores, so the panel shows what it knows and links nothing."
    : "A feature opens as project plus slug, and the graph payload carries neither, so this row is a label rather than a guess.";
}
