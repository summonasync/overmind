import type { GraphEdge, GraphField, GraphNode, GraphPosition } from "../types.ts";
import {
  type FieldForces,
  type LayoutLink,
  type LayoutNode,
  pinNode,
  seedLayoutLinks,
  seedPositions,
  settleTicksFor,
  settleWithinBudget,
  startForceLayout,
} from "./graph-layout.ts";
import { nodeKey } from "./graph-payload.ts";

// What crosses the worker boundary: the field parts the settle reads, nothing else. Positions
// are the server pins — pins stay the only server-side coordinates the ladder inherits, which is
// the spec's anti-scope restated as a wire shape.
export interface PrecomputeRequest {
  nodes: GraphNode[];
  edges: GraphEdge[];
  positions: GraphPosition[];
  width: number;
  height: number;
}

interface PreparedField {
  nodes: LayoutNode[];
  links: LayoutLink[];
  forces: FieldForces;
}

function prepare(request: PrecomputeRequest): PreparedField {
  const nodes = seedPositions(request.nodes, request.positions, request.width, request.height);
  const byKey = new Map(nodes.map((node) => [nodeKey(node.kind, node.id), node]));
  for (const position of request.positions) {
    const node = byKey.get(nodeKey(position.kind, position.id));
    if (node !== undefined) {
      // Held with the drag pin, not merely seeded: this tier runs ticks, and a server pin that
      // survives the settle is the whole point of a server pin — there is no frame clock left
      // here to reseed one that drifted.
      pinNode(node, position.x, position.y);
    }
  }
  return {
    nodes,
    links: seedLayoutLinks(request.edges, nodes),
    forces: { width: request.width, height: request.height },
  };
}

// The one-shot tier-2 settle. The worker entry calls this and nothing else, which is what keeps
// layout math on the lib side of the worker boundary.
export function precomputedPositions(request: PrecomputeRequest): LayoutNode[] {
  const { nodes, links, forces } = prepare(request);
  settleWithinBudget(nodes, links, forces);
  return nodes;
}

// Ticks per macrotask slice. The size cannot move the result — every tick reads the state the
// previous one wrote, in the order the worker runs them — which is why the fallback and the
// worker land on identical frames for the same field.
const FALLBACK_TICKS_PER_SLICE = 8;

function yieldToMain(): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

// The chunked fallback: the same bounded ladder, sliced so the main thread can paint between
// slices instead of freezing for the whole settle.
async function settleInSlices(request: PrecomputeRequest): Promise<LayoutNode[]> {
  const { nodes, links, forces } = prepare(request);
  const handle = startForceLayout(nodes, links, forces);
  const ticks = settleTicksFor(nodes.length);
  for (let done = 0; done < ticks; done += FALLBACK_TICKS_PER_SLICE) {
    const slice = Math.min(FALLBACK_TICKS_PER_SLICE, ticks - done);
    for (let tick = 0; tick < slice; tick += 1) {
      handle.simulation.tick();
    }
    await yieldToMain();
  }
  handle.stop();
  return nodes;
}

// Tier-2 transport, shipped: the chunked slices. Worker-first was the plan, and the entry that
// would serve it — graph-layout.worker.ts calling precomputedPositions — is written and pinned;
// the flip is blocked at the bundler, not in this file: with
// `new Worker(new URL("./graph-layout.worker.ts", import.meta.url))` anywhere in the module
// graph, next build (Next 16.3.5, Turbopack, output: "export") hangs forever at "Creating an
// optimized production build", while the identical tree without that one import finishes in
// about seven minutes (control run, 2026-11-22). The spec's transport contract — once, off the
// render loop, bounded, deterministic — holds in either transport; re-attempt the flip when the
// bundler can emit the worker chunk, and re-run the build probe in the same commit.
export async function precomputeLayout(
  field: GraphField,
  forces: { width: number; height: number },
): Promise<LayoutNode[]> {
  const request: PrecomputeRequest = {
    nodes: field.nodes,
    edges: field.edges,
    positions: field.positions,
    width: forces.width,
    height: forces.height,
  };
  return settleInSlices(request);
}
