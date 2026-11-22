import type { GraphNodeState } from "../types.ts";
import { anchorRadius, nodeColorName, nodeRadius, type GraphSemanticColor } from "./graph-palette.ts";
import type { LayoutLink, LayoutMode, LayoutNode } from "./graph-layout.ts";
import { nodeKey } from "./graph-payload.ts";
import { toScreenX, toScreenY, type ViewTransform } from "./graph-view.ts";

// The drawing is a function over a context-shaped object, not a method on a canvas element:
// the same code the browser runs is what `web/tests/journal-graph.test.mts` and the scale
// harness drive with a recorder, so "1200 nodes do not hang" is a measurement of this function
// rather than an assumption about a GPU. The style fields keep the DOM's own union so the real
// `CanvasRenderingContext2D` is assignable without a cast.
type PaintStyle = string | CanvasGradient | CanvasPattern;

export interface PaintTarget {
  fillStyle: PaintStyle;
  strokeStyle: PaintStyle;
  lineWidth: number;
  globalAlpha: number;
  font: string;
  clearRect: (x: number, y: number, width: number, height: number) => void;
  save: () => void;
  restore: () => void;
  beginPath: () => void;
  closePath: () => void;
  moveTo: (x: number, y: number) => void;
  lineTo: (x: number, y: number) => void;
  arc: (x: number, y: number, radius: number, start: number, end: number) => void;
  rect: (x: number, y: number, width: number, height: number) => void;
  fill: () => void;
  stroke: () => void;
  fillText: (text: string, x: number, y: number) => void;
  setLineDash: (segments: number[]) => void;
}

export const HOVER_DIM_ALPHA = 0.3;
export const SELECT_RING_ALPHA = 0.4;
export const ORPHAN_ALPHA = 0.6;
export const LABEL_SCALE_FLOOR = 0.85;
export const MAX_LABEL_CHARS = 42;

// R3's ladder, tier 2 and up. Between the floor and near zoom only hubs label, because a
// 5000-node field at arm's length turns every title into ink; a hub is a node whose link count
// cleared HUB_DEGREE_FLOOR — the degree below which a dot is a leaf in practice, not a
// landmark. The near-zoom rung stays reachable with three wheel notches from the default view
// (1.15³ ≈ 1.52), so "show me everything" is always a deliberate zoom away, never a dead end.
// Tier 1 predates the ladder and keeps the single floor it shipped with.
export const HUB_DEGREE_FLOOR = 4;
export const LABEL_SCALE_NEAR = 1.25;

export interface SceneInput {
  nodes: readonly LayoutNode[];
  links: readonly LayoutLink[];
  view: ViewTransform;
  colors: Record<GraphSemanticColor, string>;
  states: ReadonlyMap<string, GraphNodeState>;
  hoverKey: string | null;
  selectedKey: string | null;
  neighbours: ReadonlySet<string> | null;
  width: number;
  height: number;
  font: string;
  tier: LayoutMode;
  // Tier 3 only: collapsed cluster anchors, keyed by node key, carrying the member count each
  // dot stands in for. Presence switches the radius to the anchor scale and the label rule to
  // "a closed cluster always names itself"; open clusters leave their members to the ladder.
  anchors?: ReadonlyMap<string, number>;
}

export function drawScene(ctx: PaintTarget, scene: SceneInput): void {
  ctx.save();
  ctx.clearRect(0, 0, scene.width, scene.height);
  drawLinks(ctx, scene);
  drawNodes(ctx, scene);
  ctx.restore();
}

function linkAlpha(scene: SceneInput, hovered: boolean): number {
  if (scene.hoverKey === null) {
    return 1;
  }
  return hovered ? 1 : HOVER_DIM_ALPHA;
}

function touches(key: string | null, source: LayoutNode, target: LayoutNode): boolean {
  return key !== null && (keyOf(source) === key || keyOf(target) === key);
}

function drawLinks(ctx: PaintTarget, scene: SceneInput): void {
  ctx.setLineDash([]);
  for (const link of scene.links) {
    const source = link.source;
    const target = link.target;
    const hovered = touches(scene.hoverKey, source, target);
    // R3's ladder has two rungs: the hovered node's links strengthen and so do the selected
    // node's, while the 30% dim stays a hover act — R4 only promises the field dims under the
    // cursor, so a click must not fade the links it just highlighted.
    const strong = hovered || touches(scene.selectedKey, source, target);

    ctx.globalAlpha = linkAlpha(scene, hovered);
    ctx.strokeStyle = strong ? scene.colors.edgeStrong : scene.colors.edge;
    ctx.lineWidth = link.hasManual ? 1.8 : 1;
    ctx.beginPath();
    ctx.moveTo(toScreenX(source.x, scene.view), toScreenY(source.y, scene.view));
    ctx.lineTo(toScreenX(target.x, scene.view), toScreenY(target.y, scene.view));
    ctx.stroke();
  }
}

function nodeAlpha(scene: SceneInput, key: string): number {
  if (scene.hoverKey === null || scene.neighbours === null) {
    return 1;
  }
  return scene.neighbours.has(key) ? 1 : HOVER_DIM_ALPHA;
}

// One radius rule for both paint passes, so a dot never draws at one size and picks or labels
// at another: collapsed anchors wear the anchor scale, everything else the kind's own.
function radiusIn(scene: SceneInput, node: LayoutNode): number {
  const hidden = scene.anchors?.get(keyOf(node));
  return hidden === undefined ? nodeRadius(node) : anchorRadius(hidden);
}

function drawNodes(ctx: PaintTarget, scene: SceneInput): void {
  for (const node of scene.nodes) {
    const key = keyOf(node);
    const state = scene.states.get(key) ?? "linked";
    const color = scene.colors[nodeColorName(node)];
    const radius = Math.max(2, radiusIn(scene, node) * scene.view.scale);
    const x = toScreenX(node.x, scene.view);
    const y = toScreenY(node.y, scene.view);

    ctx.globalAlpha = state === "orphan" ? ORPHAN_ALPHA * nodeAlpha(scene, key) : nodeAlpha(scene, key);
    ctx.setLineDash([]);
    ctx.lineWidth = 1;
    ctx.strokeStyle = color;
    ctx.fillStyle = color;

    ctx.beginPath();
    traceShape(ctx, node, x, y, radius);
    ctx.closePath();

    // Three states, all drawn: linked is solid paint, an orphan (no link anywhere) is a hollow
    // shell, and `offView` — linked, with every link leaving this view — keeps its paint and
    // gains the dashed ring. Dropping the third from the frame is what makes a dot read as an
    // orphan it is not.
    if (state === "orphan") {
      ctx.stroke();
    } else {
      ctx.fill();
    }

    if (state === "offView") {
      ctx.setLineDash([3, 3]);
      ctx.strokeStyle = scene.colors.muted;
      ctx.beginPath();
      ctx.arc(x, y, radius + 3.5, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    if (key === scene.selectedKey) {
      ctx.globalAlpha = SELECT_RING_ALPHA * (state === "orphan" ? ORPHAN_ALPHA : 1);
      ctx.strokeStyle = scene.colors.selected;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(x, y, radius + 6, 0, Math.PI * 2);
      ctx.stroke();
    }
  }

  ctx.globalAlpha = 1;
  ctx.setLineDash([]);
  drawLabels(ctx, scene);
}

function hubDegrees(links: readonly LayoutLink[]): Map<string, number> {
  const degrees = new Map<string, number>();
  const bump = (node: LayoutNode) => {
    const key = keyOf(node);
    degrees.set(key, (degrees.get(key) ?? 0) + 1);
  };
  for (const link of links) {
    bump(link.source);
    bump(link.target);
  }
  return degrees;
}

function labelShown(scene: SceneInput, key: string, degree: number): boolean {
  if (key === scene.hoverKey || key === scene.selectedKey) {
    return true;
  }
  if (scene.anchors?.has(key)) {
    return true;
  }
  if (scene.tier === "simulate") {
    return scene.view.scale >= LABEL_SCALE_FLOOR;
  }
  if (scene.view.scale < LABEL_SCALE_FLOOR) {
    return false;
  }
  if (scene.view.scale < LABEL_SCALE_NEAR) {
    return degree >= HUB_DEGREE_FLOOR;
  }
  return true;
}

function drawLabels(ctx: PaintTarget, scene: SceneInput): void {
  ctx.font = scene.font;
  ctx.fillStyle = scene.colors.label;

  // One O(links) pass per frame, the same order drawLinks already pays; tier 1 never needs it.
  const degrees = scene.tier === "simulate" ? null : hubDegrees(scene.links);

  for (const node of scene.nodes) {
    const key = keyOf(node);
    if (!labelShown(scene, key, degrees?.get(key) ?? 0)) {
      continue;
    }

    const radius = Math.max(2, radiusIn(scene, node) * scene.view.scale);
    ctx.globalAlpha = nodeAlpha(scene, key);
    ctx.fillText(truncateLabel(node.label), toScreenX(node.x, scene.view), toScreenY(node.y, scene.view) + radius + 12);
  }

  ctx.globalAlpha = 1;
}

// A note's title is a sentence; a dot's label is a marker. Cut on word shape, not mid-token.
export function truncateLabel(label: string, max = MAX_LABEL_CHARS): string {
  const trimmed = label.trim();
  if (trimmed.length <= max) {
    return trimmed;
  }
  return `${trimmed.slice(0, max - 1).trimEnd()}…`;
}

function keyOf(node: LayoutNode): string {
  return nodeKey(node.kind, node.id);
}

// Shape is the kind, colour is the type: a project is a diamond because it is the frame the rest
// sits in, a feature is a square because it is a card on a board, a note is a dot.
function traceShape(ctx: PaintTarget, node: LayoutNode, x: number, y: number, radius: number): void {
  if (node.kind === "project") {
    ctx.moveTo(x, y - radius);
    ctx.lineTo(x + radius, y);
    ctx.lineTo(x, y + radius);
    ctx.lineTo(x - radius, y);
    return;
  }

  if (node.kind === "feature") {
    ctx.rect(x - radius * 0.85, y - radius * 0.85, radius * 1.7, radius * 1.7);
    return;
  }

  ctx.arc(x, y, radius, 0, Math.PI * 2);
}
