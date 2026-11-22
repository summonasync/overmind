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
// the spec's anti-scope restated as a wire shape. seedOrigin moves the seed spiral for tier 3's
// cluster-expansion settle (members bloom out of their anchor, not the canvas centre); absent,
// every frame is the centre spiral the tiers above ship.
export interface PrecomputeRequest {
  nodes: GraphNode[];
  edges: GraphEdge[];
  positions: GraphPosition[];
  width: number;
  height: number;
  seedOrigin?: { x: number; y: number };
  centreX?: number;
  centreY?: number;
}

interface PreparedField {
  nodes: LayoutNode[];
  links: LayoutLink[];
  forces: FieldForces;
}

function prepare(request: PrecomputeRequest): PreparedField {
  const nodes = seedPositions(
    request.nodes,
    request.positions,
    request.width,
    request.height,
    request.seedOrigin,
  );
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
    forces: {
      width: request.width,
      height: request.height,
      centreX: request.centreX,
      centreY: request.centreY,
    },
  };
}

// The one-shot tier-2 settle. The worker entry calls this and nothing else, which is what keeps
// layout math on the lib side of the worker boundary.
export function precomputedPositions(request: PrecomputeRequest): LayoutNode[] {
  const { nodes, links, forces } = prepare(request);
  settleWithinBudget(nodes, links, forces);
  return nodes;
}

// Slices are budgeted by time, not by count: one settle tick costs ~10 ms at 1500 nodes and
// ~63 ms at 5000 on the review box, so a fixed tick count would block the main thread for
// hundreds of milliseconds on big fields while yielding pointlessly on small ones. The budget
// decides only where the macrotask boundaries fall — every tick still runs, in the settle's
// order — which is why the frames stay identical to the worker entry's synchronous settle
// (test-pinned). A slice ends after ~50 ms of work or 8 ticks, whichever comes first, so the
// worst slice overshoots the budget by one long tick; the tick ceiling keeps small fields from
// spending their whole settle on yields.
const SLICE_TIME_BUDGET_MS = 50;
const SLICE_TICK_CEILING = 8;

function yieldToMain(): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

// The chunked transport: the same bounded ladder, sliced so the main thread can paint between
// slices instead of freezing for the whole settle. The abandon token is checked between slices
// and never inside one: dropping a settle removes whole slices from the tail, so the tick order
// any completed settle runs — and the frame it lands on — is exactly the un-aborted run's
// (test-pinned).
async function settleInSlices(
  request: PrecomputeRequest,
  isAbandoned: () => boolean,
): Promise<LayoutNode[]> {
  const { nodes, links, forces } = prepare(request);
  const handle = startForceLayout(nodes, links, forces);
  const ticks = settleTicksFor(nodes.length);
  let done = 0;
  while (done < ticks) {
    if (isAbandoned()) {
      break;
    }
    const sliceStart = performance.now();
    let sliceTicks = 0;
    while (done < ticks && sliceTicks < SLICE_TICK_CEILING) {
      handle.simulation.tick();
      done += 1;
      sliceTicks += 1;
      if (performance.now() - sliceStart >= SLICE_TIME_BUDGET_MS) {
        break;
      }
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
  forces: { width: number; height: number; centreX?: number; centreY?: number },
  isAbandoned: () => boolean = () => false,
  seedOrigin?: { x: number; y: number },
): Promise<LayoutNode[]> {
  const request: PrecomputeRequest = {
    nodes: field.nodes,
    edges: field.edges,
    positions: field.positions,
    width: forces.width,
    height: forces.height,
    centreX: forces.centreX,
    centreY: forces.centreY,
    seedOrigin,
  };
  return settleInSlices(request, isAbandoned);
}

// When the landed frame swaps in, a drag that began on the seed is mid-flight: the owner is
// holding a node the settle never saw, and swapping the arrays under it would teleport the dot
// back to its settled coordinate. A held node keeps its hold, and its coordinate is the hold —
// the hold is what the owner aimed at, whether the drag had time to move x/y or not. Every
// other node takes its settled one. Server pins need none of this — prepare() already held
// them through the settle.
export function preserveHeldNodes(
  settled: readonly LayoutNode[],
  standing: readonly LayoutNode[],
): void {
  const held = new Map<string, LayoutNode>();
  for (const node of standing) {
    if (node.fx != null) {
      held.set(nodeKey(node.kind, node.id), node);
    }
  }
  for (const node of settled) {
    const kept = held.get(nodeKey(node.kind, node.id));
    if (kept !== undefined) {
      node.x = kept.fx ?? kept.x;
      node.y = kept.fy ?? kept.y;
      node.fx = kept.fx;
      node.fy = kept.fy;
    }
  }
}
