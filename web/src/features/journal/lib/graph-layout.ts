import {
  forceCollide,
  forceLink,
  forceManyBody,
  forceSimulation,
  forceX,
  forceY,
  type Simulation,
  type SimulationLinkDatum,
  type SimulationNodeDatum,
} from "d3-force";
import type { GraphEdge, GraphNode, GraphPosition } from "../types.ts";
import { nodeKey } from "./graph-payload.ts";

// R9's tier-1 ceiling. Above it the route stops simulating and shows a settled field with the
// real count in the UI; tiers 2 and 3 are a spawned card, not a mode switch hidden in here.
export const SIM_CEILING = 1000;

// d3 mutates the datum it is handed, so the canvas keeps its own node type and the payload's
// stays clean — the same array React Query owns would otherwise carry coordinates nobody asked
// for and the cache would start lying after one drag.
export interface LayoutNode extends GraphNode, SimulationNodeDatum {
  x: number;
  y: number;
}

export interface LayoutLink extends SimulationLinkDatum<LayoutNode> {
  source: LayoutNode;
  target: LayoutNode;
  hasManual: boolean;
}

export type LayoutMode = "simulate" | "settled";

export function isOverCeiling(nodeCount: number): boolean {
  return nodeCount > SIM_CEILING;
}

export function layoutModeFor(nodeCount: number): LayoutMode {
  return isOverCeiling(nodeCount) ? "settled" : "simulate";
}

const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5));
const SEED_RADIUS_STEP = 9;

// A deterministic first frame: the same payload must land on the same dots after a reload, or
// "filters survive reload" would mean "the field re-scatters every time". Pinned positions
// (R5's rows, empty until T8 ships them) win over the seed for the node they name.
export function seedPositions(
  nodes: readonly GraphNode[],
  positions: readonly GraphPosition[],
  width: number,
  height: number,
): LayoutNode[] {
  const pinned = new Map<string, GraphPosition>();
  for (const position of positions) {
    pinned.set(nodeKey(position.kind, position.id), position);
  }

  const centerX = width / 2;
  const centerY = height / 2;

  return nodes.map((node, index) => {
    const pin = pinned.get(nodeKey(node.kind, node.id));
    if (pin !== undefined) {
      return { ...node, x: pin.x, y: pin.y };
    }
    const radius = SEED_RADIUS_STEP * Math.sqrt(index + 0.5);
    const angle = index * GOLDEN_ANGLE;
    return { ...node, x: centerX + radius * Math.cos(angle), y: centerY + radius * Math.sin(angle) };
  });
}

export function seedLayoutLinks(
  edges: readonly GraphEdge[],
  layout: readonly LayoutNode[],
): LayoutLink[] {
  const byKey = new Map<string, LayoutNode>();
  for (const node of layout) {
    byKey.set(nodeKey(node.kind, node.id), node);
  }

  const links: LayoutLink[] = [];
  for (const edge of edges) {
    const source = byKey.get(nodeKey(edge.fromKind, edge.fromId));
    const target = byKey.get(nodeKey(edge.toKind, edge.toId));
    if (source === undefined || target === undefined) {
      continue;
    }
    links.push({ source, target, hasManual: edge.hasManual });
  }
  return links;
}

// Small and reproducible; d3's own jiggle is the only randomness in the simulation and this
// replaces it, so a reload of an unchanged payload settles where it settled before.
export function seededRandom(seed = 20261121): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export interface FieldForces {
  width: number;
  height: number;
  repulsion?: number;
}

export function applyFieldForces(
  simulation: Simulation<LayoutNode, LayoutLink>,
  links: readonly LayoutLink[],
  forces: FieldForces,
): Simulation<LayoutNode, LayoutLink> {
  return simulation
    .force("link", forceLink<LayoutNode, LayoutLink>([...links]).distance(46).strength(0.35))
    .force("charge", forceManyBody().strength(forces.repulsion ?? -60))
    .force("collide", forceCollide(14))
    .force("centerX", forceX(forces.width / 2).strength(0.04))
    .force("centerY", forceY(forces.height / 2).strength(0.06));
}

export interface LayoutHandle {
  simulation: Simulation<LayoutNode, LayoutLink>;
  stop: () => void;
}

// Created stopped: the frame clock is the component's business (it owns the requestAnimationFrame
// loop, the reduced-motion branch and the ceiling branch), and a simulation that starts itself
// here is a second animation loop nobody asked for.
export function startForceLayout(
  nodes: LayoutNode[],
  links: readonly LayoutLink[],
  forces: FieldForces,
): LayoutHandle {
  const simulation = forceSimulation(nodes).randomSource(seededRandom());
  applyFieldForces(simulation, links, forces);
  simulation.stop();
  return { simulation, stop: () => simulation.stop() };
}

// Reduced motion and the over-ceiling settled frame both mean "no animation", and the cheap way
// to honour it is to run the same forces in one synchronous pass instead of on a frame clock.
// That pass is bounded by node count, because a synchronous settle is main-thread work: measured
// on this box at 999 nodes one tick costs about 11 ms, so an unbounded 300-tick convergence would
// block for ~1.8 s — a hang wearing an accessibility badge. The seed spiral is already a legible
// layout, so a bounded refinement improves it rather than compromising it, and the bound is a
// count and not a clock: the same field settles to the same frame on every reload.
export const SETTLE_TICKS = 300;

export function settleTicksFor(nodeCount: number): number {
  if (nodeCount <= 100) return 300;
  if (nodeCount <= 300) return 100;
  if (nodeCount <= 600) return 40;
  return 20;
}

export function settleLayout(
  nodes: LayoutNode[],
  links: LayoutLink[],
  forces: FieldForces,
  ticks = SETTLE_TICKS,
): void {
  const handle = startForceLayout(nodes, links, forces);
  for (let tick = 0; tick < ticks; tick += 1) {
    handle.simulation.tick();
  }
  handle.stop();
}

export function settleWithinBudget(
  nodes: LayoutNode[],
  links: LayoutLink[],
  forces: FieldForces,
): void {
  settleLayout(nodes, links, forces, settleTicksFor(nodes.length));
}

// Over the ceiling nothing is simulated at all: the seed spiral is the frame, which is O(n) and
// keeps the first paint at the same cost the payload already paid. The UI states the count.
export function settledField(
  nodes: readonly GraphNode[],
  positions: readonly GraphPosition[],
  width: number,
  height: number,
): LayoutNode[] {
  return seedPositions(nodes, positions, width, height);
}

// d3 reads `fx`/`fy` off the datum it is handed — that is the library's documented pinning
// surface — so the mutation lives here, next to the type it belongs to, instead of being spelled
// out in a React component where the compiler is entitled to ask what is mutating what.
export function pinNode(node: LayoutNode, x: number, y: number): void {
  node.fx = x;
  node.fy = y;
}

export function releasePinnedNodes(nodes: readonly LayoutNode[]): void {
  for (const node of nodes) {
    node.fx = null;
    node.fy = null;
  }
}

export function reheat(simulation: Simulation<LayoutNode, LayoutLink>, alpha: number): void {
  simulation.alpha(Math.max(simulation.alpha(), alpha));
}

export interface Neighbourhood {
  keys: Set<string>;
}

// The hover rule is "dim the rest of the field to 30%", which needs the neighbourhood, not a
// per-frame scan of every edge.
export function buildNeighbourhoods(links: readonly LayoutLink[]): Map<string, Neighbourhood> {
  const map = new Map<string, Neighbourhood>();
  const touch = (key: string, other: string) => {
    let entry = map.get(key);
    if (entry === undefined) {
      entry = { keys: new Set<string>() };
      map.set(key, entry);
    }
    entry.keys.add(other);
  };

  for (const link of links) {
    const source = link.source as LayoutNode;
    const target = link.target as LayoutNode;
    touch(nodeKey(source.kind, source.id), nodeKey(target.kind, target.id));
    touch(nodeKey(target.kind, target.id), nodeKey(source.kind, source.id));
  }

  return map;
}

export function neighbourhoodOf(
  neighbourhoods: Map<string, Neighbourhood>,
  key: string | null,
): Set<string> | null {
  if (key === null) {
    return null;
  }
  const entry = neighbourhoods.get(key);
  if (entry === undefined) {
    return new Set<string>([key]);
  }
  entry.keys.add(key);
  return entry.keys;
}
