export interface ViewTransform {
  scale: number;
  tx: number;
  ty: number;
}

export const MIN_SCALE = 0.2;
export const MAX_SCALE = 4;

export const IDENTITY_VIEW: ViewTransform = { scale: 1, tx: 0, ty: 0 };

export function toScreenX(x: number, view: ViewTransform): number {
  return x * view.scale + view.tx;
}

export function toScreenY(y: number, view: ViewTransform): number {
  return y * view.scale + view.ty;
}

export function toWorldX(x: number, view: ViewTransform): number {
  return (x - view.tx) / view.scale;
}

export function toWorldY(y: number, view: ViewTransform): number {
  return (y - view.ty) / view.scale;
}

export function panBy(view: ViewTransform, dx: number, dy: number): ViewTransform {
  return { scale: view.scale, tx: view.tx + dx, ty: view.ty + dy };
}

// Wheel zoom keeps the point under the cursor where the cursor is; zooming about the canvas
// centre makes the field slide away from whatever the owner was looking at.
export function zoomAt(view: ViewTransform, factor: number, atX: number, atY: number): ViewTransform {
  const scale = clampScale(view.scale * factor);
  const worldX = toWorldX(atX, view);
  const worldY = toWorldY(atY, view);
  return { scale, tx: atX - worldX * scale, ty: atY - worldY * scale };
}

export function clampScale(scale: number): number {
  if (!Number.isFinite(scale)) {
    return MIN_SCALE;
  }
  return Math.min(MAX_SCALE, Math.max(MIN_SCALE, scale));
}

interface SpreadPoint {
  x: number;
  y: number;
}

// Fit is measured in world units with the node radius already folded into the bounds, so "fit"
// never clips the ring of the dot on the edge of the frame.
export function fitView(
  points: readonly SpreadPoint[],
  width: number,
  height: number,
  padding = 56,
): ViewTransform {
  if (points.length === 0 || width <= 0 || height <= 0) {
    return { ...IDENTITY_VIEW };
  }

  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const point of points) {
    if (!Number.isFinite(point.x) || !Number.isFinite(point.y)) {
      continue;
    }
    minX = Math.min(minX, point.x);
    minY = Math.min(minY, point.y);
    maxX = Math.max(maxX, point.x);
    maxY = Math.max(maxY, point.y);
  }

  if (!Number.isFinite(minX) || !Number.isFinite(minY)) {
    return { ...IDENTITY_VIEW };
  }

  const spanX = Math.max(maxX - minX, 1);
  const spanY = Math.max(maxY - minY, 1);
  const scale = clampScale(
    Math.min((width - padding * 2) / spanX, (height - padding * 2) / spanY),
  );

  return {
    scale,
    tx: (width - spanX * scale) / 2 - minX * scale,
    ty: (height - spanY * scale) / 2 - minY * scale,
  };
}

// A pick is a distance test in world units with the node's own radius plus a screen-padded
// allowance: at 24 px of hit box the smallest dot stays clickable when the field is zoomed out,
// which is the pointer-target floor R7 puts on new surfaces.
export interface PickablePoint extends SpreadPoint {
  key: string;
  radius: number;
}

export const MIN_HIT_RADIUS = 12;

export function hitRadiusFor(radius: number, scale: number): number {
  return Math.max(radius, MIN_HIT_RADIUS / Math.max(scale, 0.01));
}

export function pickNode(
  points: readonly PickablePoint[],
  worldX: number,
  worldY: number,
  scale: number,
): string | null {
  let best: string | null = null;
  let bestDistance = Infinity;

  for (const point of points) {
    const reach = hitRadiusFor(point.radius, scale);
    const dx = point.x - worldX;
    const dy = point.y - worldY;
    const distance = Math.hypot(dx, dy);
    if (distance <= reach && distance < bestDistance) {
      bestDistance = distance;
      best = point.key;
    }
  }

  return best;
}

export function scalePercent(scale: number): string {
  return `${Math.round(scale * 100)}%`;
}

// The canvas' own keyboard path: arrows walk the node list in payload order, which is the
// server's stable order (kind, label, id), so the same arrow press lands on the same dot for
// two readers on the same field. `null` means "nothing selected yet", and the first press lands
// on the first node rather than on nothing.
export function nextNodeIndex(current: number | null, delta: number, length: number): number | null {
  if (length <= 0) {
    return null;
  }
  if (current === null || current < 0 || current >= length) {
    return delta < 0 ? length - 1 : 0;
  }
  return (current + delta + length) % length;
}
