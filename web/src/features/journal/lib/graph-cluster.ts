import type { GraphEdge, GraphField, GraphNode } from "../types.ts";
import {
  type LayoutNode,
  seedPositions,
} from "./graph-layout.ts";
import { MAX_LABEL_CHARS, truncateLabel } from "./graph-paint.ts";
import { nodeKey } from "./graph-payload.ts";

// Tier 3 groups the field by the project each node already carries, so the canvas past the
// precompute ceiling draws one anchor per project instead of one dot per node. The grouping
// reads only the payload — no server call, no new endpoint: the same one-request whole-field
// contract the tiers above run on.
//
// The unsorted bucket is explicit rather than implicit because a collapsed dot must say what it
// stands for. A node lands there when its projectId is empty, is the all-zero guid the Host
// serialises for "no project", or names a project whose dot is not in this view — in all three
// cases there is no on-field project dot to anchor to and no label the canvas could honestly
// print, so those nodes share one door instead of pretending to be sorted. Real project ids are
// guids, so a plain word cannot collide with one.
export const UNSORTED_CLUSTER_ID = "unsorted";

const ZERO_GUID = "00000000-0000-0000-0000-000000000000";

const UNSORTED_ANCHOR: GraphNode = {
  kind: "project",
  id: UNSORTED_CLUSTER_ID,
  label: "Unsorted",
  projectId: UNSORTED_CLUSTER_ID,
  entryType: null,
  featureStatus: null,
  orphan: false,
};

export function isUnassignedProject(projectId: string): boolean {
  return projectId.length === 0 || projectId === ZERO_GUID;
}

// One group per project dot the payload carries, in the payload's own project order — the Host
// orders projects by CreatedAt then Id, so the group order survives a live-sync refetch beat
// that reshuffles entry order. Everything not anchorable to one of those dots joins the single
// unsorted group, kept last so the named map reads first.
export interface ClusterGroup {
  key: string;
  anchorKey: string;
  anchor: GraphNode;
  members: GraphNode[];
  // What the collapsed dot stands in for: the group minus the anchor itself, which stays
  // visible. A project dot with nothing behind it hides nothing, collapses nothing, and keeps
  // its plain name — it is a dot, not a door.
  hiddenCount: number;
}

export interface ClusterModel {
  groups: ClusterGroup[];
  clusterOfMember: Map<string, string>;
}

export function buildClusters(field: GraphField): ClusterModel {
  const named = new Map<string, ClusterGroup>();
  for (const node of field.nodes) {
    if (node.kind !== "project") {
      continue;
    }
    named.set(node.id, {
      key: node.id,
      anchorKey: nodeKey(node.kind, node.id),
      anchor: node,
      members: [node],
      hiddenCount: 0,
    });
  }

  const unsorted: GraphNode[] = [];
  const clusterOfMember = new Map<string, string>();
  for (const node of field.nodes) {
    const key = nodeKey(node.kind, node.id);
    if (node.kind === "project") {
      clusterOfMember.set(key, node.id);
      continue;
    }
    const group = !isUnassignedProject(node.projectId) ? named.get(node.projectId) : undefined;
    if (group === undefined) {
      unsorted.push(node);
      clusterOfMember.set(key, UNSORTED_CLUSTER_ID);
    } else {
      group.members.push(node);
      clusterOfMember.set(key, group.key);
    }
  }

  const groups = [...named.values()];
  for (const group of groups) {
    group.hiddenCount = group.members.length - 1;
  }
  if (unsorted.length > 0) {
    groups.push({
      key: UNSORTED_CLUSTER_ID,
      anchorKey: nodeKey(UNSORTED_ANCHOR.kind, UNSORTED_ANCHOR.id),
      anchor: UNSORTED_ANCHOR,
      members: unsorted,
      hiddenCount: unsorted.length,
    });
  }
  return { groups, clusterOfMember };
}

// The collapsed label carries the count, and the count must survive the painter's truncation:
// cutting "A very long project name (1247)" at forty-two characters would print a cluster that
// promises nothing. The name gets the budget the suffix leaves, so the whole label still fits
// one truncation pass and the number on the frame is the number of dots behind it.
export function clusterLabel(name: string, hiddenCount: number): string {
  const suffix = ` (${hiddenCount})`;
  return `${truncateLabel(name, MAX_LABEL_CHARS - suffix.length)}${suffix}`;
}

// Member edges folded onto the anchor pair they cross, once per unordered pair. Direction is
// payload noise at this zoom — an edge a→b and its b→a twin are the same line between two
// clusters — so the pair is keyed on its sorted node keys and the count is what survives.
export interface AnchorLinkRow {
  from: GraphNode;
  to: GraphNode;
  count: number;
  hasManual: boolean;
}

interface PairKey {
  low: string;
  high: string;
}

function pairOf(a: GraphNode, b: GraphNode): PairKey {
  const left = nodeKey(a.kind, a.id);
  const right = nodeKey(b.kind, b.id);
  return left <= right ? { low: left, high: right } : { low: right, high: left };
}

function aggregateAnchors(
  field: GraphField,
  model: ClusterModel,
  expanded: ReadonlySet<string>,
): AnchorLinkRow[] {
  const anchorOf = new Map<string, GraphNode>();
  for (const group of model.groups) {
    anchorOf.set(group.key, group.anchor);
  }
  const byKey = new Map(field.nodes.map((node) => [nodeKey(node.kind, node.id), node]));

  const rows = new Map<string, AnchorLinkRow>();
  for (const edge of field.edges) {
    const fromKey = nodeKey(edge.fromKind, edge.fromId);
    const toKey = nodeKey(edge.toKind, edge.toId);
    const fromCluster = model.clusterOfMember.get(fromKey);
    const toCluster = model.clusterOfMember.get(toKey);
    if (fromCluster === undefined || toCluster === undefined) {
      continue;
    }
    const from = expanded.has(fromCluster) ? byKey.get(fromKey)! : anchorOf.get(fromCluster)!;
    const to = expanded.has(toCluster) ? byKey.get(toKey)! : anchorOf.get(toCluster)!;
    if (from === to) {
      continue;
    }
    const pair = pairOf(from, to);
    const id = `${pair.low}|${pair.high}`;
    const row = rows.get(id);
    if (row === undefined) {
      rows.set(id, { from, to, count: 1, hasManual: edge.hasManual });
    } else {
      row.count += 1;
      row.hasManual = row.hasManual || edge.hasManual;
    }
  }
  return [...rows.values()];
}

// The all-collapsed slice of the projection: every edge folded onto the anchor pair it crosses.
export function anchorLinks(field: GraphField, model: ClusterModel): AnchorLinkRow[] {
  return aggregateAnchors(field, model, new Set<string>());
}

export function anchorLinkEdges(rows: readonly AnchorLinkRow[]): GraphEdge[] {
  return rows.map((row) => ({
    fromKind: row.from.kind,
    fromId: row.from.id,
    toKind: row.to.kind,
    toId: row.to.id,
    hasManual: row.hasManual,
  }));
}

// What the field draws right now: an endpoint whose cluster is open is itself, an endpoint
// behind a closed cluster is that cluster's anchor. Self-lines vanish — an edge inside one
// closed cluster has nowhere to go — and they come back as themselves when the cluster opens.
export function projectedLinks(
  field: GraphField,
  model: ClusterModel,
  expanded: ReadonlySet<string>,
): GraphEdge[] {
  return aggregateAnchors(field, model, expanded).map((row) => ({
    fromKind: row.from.kind,
    fromId: row.from.id,
    toKind: row.to.kind,
    toId: row.to.id,
    hasManual: row.hasManual,
  }));
}

export interface ClusterView {
  nodes: GraphNode[];
  // Collapsed anchors only, keyed by anchor nodeKey: the painter sizes the dot and prints the
  // count from this map, and everything absent from it paints by the ordinary rules.
  hidden: Map<string, number>;
}

// The visible field: every anchor, plus the members of open clusters with their honest labels
// restored. The anchor is emitted once — for a named group it is also a member, and doubling it
// would draw two dots for one node.
export function visibleNodes(
  model: ClusterModel,
  expanded: ReadonlySet<string>,
): ClusterView {
  const nodes: GraphNode[] = [];
  const hidden = new Map<string, number>();
  for (const group of model.groups) {
    const open = expanded.has(group.key);
    nodes.push(
      open || group.hiddenCount === 0
        ? group.anchor
        : { ...group.anchor, label: clusterLabel(group.anchor.label, group.hiddenCount) },
    );
    if (!open) {
      if (group.hiddenCount > 0) {
        hidden.set(group.anchorKey, group.hiddenCount);
      }
      continue;
    }
    for (const member of group.members) {
      if (member !== group.anchor) {
        nodes.push(member);
      }
    }
  }
  return { nodes, hidden };
}

// The sub-field a cluster's expansion settles: the group's own nodes, the edges that stay
// inside it, and the server pins that name its members — a pinned member holds its row's spot
// through the bloom, exactly as it would in any tier. Seeding starts from the anchor's standing
// coordinate, which the caller passes as the settle's seedOrigin.
export function clusterSubField(field: GraphField, group: ClusterGroup): GraphField {
  const keys = new Set(group.members.map((member) => nodeKey(member.kind, member.id)));
  return {
    nodes: group.members.slice(),
    edges: field.edges.filter(
      (edge) =>
        keys.has(nodeKey(edge.fromKind, edge.fromId)) && keys.has(nodeKey(edge.toKind, edge.toId)),
    ),
    positions: field.positions.filter((position) => keys.has(nodeKey(position.kind, position.id))),
    counts: field.counts,
    unsupported: field.unsupported,
  };
}

// The anchor layer's own field: one node per cluster joined by the aggregated anchor links —
// the small set the force machinery actually settles, whatever the payload's node count is.
export function anchorSetField(field: GraphField, model: ClusterModel): GraphField {
  const anchors = model.groups.map((group) => group.anchor);
  const anchorKeys = new Set(anchors.map((anchor) => nodeKey(anchor.kind, anchor.id)));
  return {
    nodes: anchors,
    edges: anchorLinkEdges(anchorLinks(field, model)),
    positions: field.positions.filter((position) =>
      anchorKeys.has(nodeKey(position.kind, position.id)),
    ),
    counts: field.counts,
    unsupported: field.unsupported,
  };
}

// The composition the canvas draws: anchors at their settled coordinates (server pins win
// through the seed, the anchor settle's frame wins over the spiral), open groups' members
// around the anchor they bloomed from. Coordinates the field already shows are kept — a
// refetch or a neighbour's expansion must not move a dot the owner is looking at.
export function composeClusterView(
  field: GraphField,
  model: ClusterModel,
  expanded: ReadonlySet<string>,
  anchorCoords: ReadonlyMap<string, { x: number; y: number }>,
  standing: ReadonlyMap<string, LayoutNode>,
  width: number,
  height: number,
): { nodes: LayoutNode[]; hidden: Map<string, number> } {
  const view = visibleNodes(model, expanded);
  const anchorCount = model.groups.length;
  const seededAnchors = seedPositions(view.nodes.slice(0, anchorCount), field.positions, width, height);

  // Anchor coordinates resolve for every group before anything is placed: the settled frame
  // wins where it has landed, and the seed spiral covers the rest — a cluster opened before the
  // anchor settle finished must still bloom around its dot, not vanish waiting for it.
  const coords = new Map<string, { x: number; y: number }>();
  for (const anchor of seededAnchors) {
    coords.set(nodeKey(anchor.kind, anchor.id), { x: anchor.x, y: anchor.y });
  }
  for (const [key, at] of anchorCoords) {
    coords.set(key, at);
  }

  // A dot the owner is holding keeps its hold through any recomposition — that is the drag they
  // are aiming, not a coordinate to be corrected. Anchors then take their resolved coordinate
  // (the seed spiral must lose to a landed settle or it would never visibly land); everything
  // else keeps whatever the field already shows and falls back to its seed.
  const place = (node: LayoutNode, settled?: { x: number; y: number }): LayoutNode => {
    const before = standing.get(nodeKey(node.kind, node.id));
    if (before !== undefined && before.fx != null) {
      return { ...node, x: before.fx ?? before.x, y: before.fy ?? before.y, fx: before.fx, fy: before.fy };
    }
    if (settled !== undefined) {
      return { ...node, x: settled.x, y: settled.y };
    }
    return before === undefined ? node : { ...node, x: before.x, y: before.y };
  };

  const nodes: LayoutNode[] = [];
  for (const anchor of seededAnchors) {
    nodes.push(place(anchor, coords.get(nodeKey(anchor.kind, anchor.id))));
  }

  for (const group of model.groups) {
    if (!expanded.has(group.key)) {
      continue;
    }
    const members = group.members.filter((member) => member !== group.anchor);
    for (const member of seedPositions(members, field.positions, width, height, coords.get(group.anchorKey))) {
      nodes.push(place(member));
    }
  }
  return { nodes, hidden: view.hidden };
}
