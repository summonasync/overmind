import type {
  GraphEdge,
  GraphEntryType,
  GraphFeatureStatus,
  GraphField,
  GraphNode,
  GraphNodeKind,
  GraphNodeState,
  GraphPosition,
} from "../types.ts";

// The wire is `unknown` until this module says otherwise, and the saying is the product's
// posture: the three node kinds, the camelCase spellings the Host's enum converter emits, and
// nothing else. A fourth kind arrives as a count in `unsupported`, never as a dot.
const NODE_KINDS: readonly GraphNodeKind[] = ["entry", "project", "feature"];
const ENTRY_TYPES: readonly GraphEntryType[] = ["journal", "decision", "idea"];
const FEATURE_STATUSES: readonly GraphFeatureStatus[] = [
  "draft",
  "ready",
  "inProgress",
  "review",
  "done",
];

export function nodeKey(kind: GraphNodeKind, id: string): string {
  return `${kind}:${id}`;
}

export function asGraphNodeKind(raw: unknown): GraphNodeKind | null {
  return NODE_KINDS.find((kind) => kind === raw) ?? null;
}

function asText(raw: unknown): string {
  return typeof raw === "string" ? raw : "";
}

function asNumber(raw: unknown): number {
  return typeof raw === "number" && Number.isFinite(raw) ? raw : 0;
}

function asEntryType(raw: unknown): GraphEntryType | null {
  return ENTRY_TYPES.find((type) => type === raw) ?? null;
}

function asFeatureStatus(raw: unknown): GraphFeatureStatus | null {
  return FEATURE_STATUSES.find((status) => status === raw) ?? null;
}

function asRows(raw: unknown): unknown[] {
  return Array.isArray(raw) ? raw : [];
}

function readNode(raw: unknown): GraphNode | null {
  if (raw === null || typeof raw !== "object") {
    return null;
  }
  const row = raw as Record<string, unknown>;
  const kind = asGraphNodeKind(row.kind);
  const id = asText(row.id);
  if (kind === null || id.length === 0) {
    return null;
  }

  return {
    kind,
    id,
    label: asText(row.label),
    projectId: asText(row.projectId),
    entryType: asEntryType(row.entryType),
    featureStatus: asFeatureStatus(row.featureStatus),
    orphan: row.orphan === true,
  };
}

function readEdge(raw: unknown): GraphEdge | null {
  if (raw === null || typeof raw !== "object") {
    return null;
  }
  const row = raw as Record<string, unknown>;
  const fromKind = asGraphNodeKind(row.fromKind);
  const toKind = asGraphNodeKind(row.toKind);
  const fromId = asText(row.fromId);
  const toId = asText(row.toId);
  if (fromKind === null || toKind === null || fromId.length === 0 || toId.length === 0) {
    return null;
  }
  return { fromKind, fromId, toKind, toId, hasManual: row.hasManual === true };
}

function readPosition(raw: unknown): GraphPosition | null {
  if (raw === null || typeof raw !== "object") {
    return null;
  }
  const row = raw as Record<string, unknown>;
  const kind = asGraphNodeKind(row.kind);
  const id = asText(row.id);
  if (kind === null || id.length === 0) {
    return null;
  }
  return { kind, id, x: asNumber(row.x), y: asNumber(row.y) };
}

function readCounts(raw: unknown): GraphField["counts"] {
  const row = (raw !== null && typeof raw === "object" ? raw : {}) as Record<string, unknown>;
  return {
    nodes: asNumber(row.nodes),
    edges: asNumber(row.edges),
    orphans: asNumber(row.orphans),
    entryNodes: asNumber(row.entryNodes),
    projectNodes: asNumber(row.projectNodes),
    featureNodes: asNumber(row.featureNodes),
    isolatedInView: asNumber(row.isolatedInView),
  };
}

export function emptyGraphField(): GraphField {
  return {
    nodes: [],
    edges: [],
    positions: [],
    counts: {
      nodes: 0,
      edges: 0,
      orphans: 0,
      entryNodes: 0,
      projectNodes: 0,
      featureNodes: 0,
      isolatedInView: 0,
    },
    unsupported: { nodes: 0, edges: 0 },
  };
}

// An edge is drawable only when both of its endpoints are dots on this canvas. The endpoint
// rule is the server's (`GraphReader.BuildEdgesAsync`), and the payload can still disagree with
// it after a focus window trims nodes, so the canvas checks instead of trusting and drawing a
// line into the void.
export function normalizeGraphField(raw: unknown): GraphField {
  if (raw === null || typeof raw !== "object") {
    return emptyGraphField();
  }
  const body = raw as Record<string, unknown>;

  const nodes: GraphNode[] = [];
  let unsupportedNodes = 0;
  for (const row of asRows(body.nodes)) {
    const node = readNode(row);
    if (node === null) {
      unsupportedNodes += 1;
      continue;
    }
    nodes.push(node);
  }

  const present = new Set(nodes.map((node) => nodeKey(node.kind, node.id)));
  const edges: GraphEdge[] = [];
  let unsupportedEdges = 0;
  for (const row of asRows(body.edges)) {
    const edge = readEdge(row);
    if (
      edge === null ||
      !present.has(nodeKey(edge.fromKind, edge.fromId)) ||
      !present.has(nodeKey(edge.toKind, edge.toId))
    ) {
      unsupportedEdges += 1;
      continue;
    }
    edges.push(edge);
  }

  const positions: GraphPosition[] = [];
  for (const row of asRows(body.positions)) {
    const position = readPosition(row);
    if (position !== null && present.has(nodeKey(position.kind, position.id))) {
      positions.push(position);
    }
  }

  return {
    nodes,
    edges,
    positions,
    counts: readCounts(body.counts),
    unsupported: { nodes: unsupportedNodes, edges: unsupportedEdges },
  };
}

export interface EdgeEndpoints {
  from: string;
  to: string;
}

export function edgeEndpoints(edge: GraphEdge): EdgeEndpoints {
  return {
    from: nodeKey(edge.fromKind, edge.fromId),
    to: nodeKey(edge.toKind, edge.toId),
  };
}

// The three states the legend explains. `offView` is the dot the payload cannot draw a line
// from: the projection's own orphan flag says it is linked, and no rendered edge names it, so
// every real link leaves this view. Derived from the same two fields the server's
// `isolatedInView` count is derived from, which is what makes `linkedNodeKeys` below testable
// against the shipped number rather than against a second definition.
export function graphNodeStates(
  nodes: readonly GraphNode[],
  edges: readonly GraphEdge[],
): Map<string, GraphNodeState> {
  const linked = linkedNodeKeys(edges);
  const states = new Map<string, GraphNodeState>();
  for (const node of nodes) {
    const key = nodeKey(node.kind, node.id);
    if (node.orphan) {
      states.set(key, "orphan");
    } else {
      states.set(key, linked.has(key) ? "linked" : "offView");
    }
  }
  return states;
}

export function linkedNodeKeys(edges: readonly GraphEdge[]): Set<string> {
  const linked = new Set<string>();
  for (const edge of edges) {
    const { from, to } = edgeEndpoints(edge);
    linked.add(from);
    linked.add(to);
  }
  return linked;
}

// The client's reading of `counts.isolatedInView`: NODES flagged linked with no rendered edge,
// a node with two off-view links counted once. Kept as a function so a test can put it next to
// the server's number and catch a drift in either direction.
export function isolatedInViewNodes(
  nodes: readonly GraphNode[],
  edges: readonly GraphEdge[],
): number {
  const linked = linkedNodeKeys(edges);
  return nodes.filter((node) => !node.orphan && !linked.has(nodeKey(node.kind, node.id))).length;
}
