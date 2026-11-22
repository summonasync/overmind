import type { LayoutNode } from "./graph-layout.ts";
import { type PrecomputeRequest, precomputedPositions } from "./graph-precompute.ts";

// Thin by contract: the settle lives in the lib, so this boundary carries transport and nothing
// else — receive the field parts, call, post the frame back. A source pin in
// journal-graph.test.mts refuses d3 and trigonometry here so the rule outlives the review.
// Deliberately not imported yet: the bundler hangs on emitting this chunk for the static export,
// so the shipped transport is the chunked slices and this entry waits for that flip (see the
// transport comment in graph-precompute.ts).
const scope = self as unknown as {
  onmessage: ((event: MessageEvent<PrecomputeRequest>) => void) | null;
  postMessage: (positions: LayoutNode[]) => void;
};

scope.onmessage = (event) => {
  scope.postMessage(precomputedPositions(event.data));
};
