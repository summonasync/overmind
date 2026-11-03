export interface PanelGeometry {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface PanelSize {
  width: number;
  height: number;
}

export interface ViewportSize {
  width: number;
  height: number;
}

export const PANEL_MIN_SIZE = 320;
export const PANEL_VIEWPORT_MARGIN = 8;

export function readViewportSize(): ViewportSize {
  return { width: window.innerWidth, height: window.innerHeight };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export function clampPanelSize(size: PanelSize, viewport: ViewportSize): PanelSize {
  const maxWidth = Math.max(PANEL_MIN_SIZE, viewport.width - PANEL_VIEWPORT_MARGIN * 2);
  const maxHeight = Math.max(PANEL_MIN_SIZE, viewport.height - PANEL_VIEWPORT_MARGIN * 2);
  return {
    width: clamp(size.width, PANEL_MIN_SIZE, maxWidth),
    height: clamp(size.height, PANEL_MIN_SIZE, maxHeight),
  };
}

export function clampPanelPosition(
  position: { x: number; y: number },
  size: PanelSize,
  viewport: ViewportSize,
): { x: number; y: number } {
  return {
    x: clamp(position.x, 0, Math.max(0, viewport.width - size.width)),
    y: clamp(position.y, 0, Math.max(0, viewport.height - size.height)),
  };
}

export function clampPanelGeometry(geometry: PanelGeometry, viewport: ViewportSize): PanelGeometry {
  const size = clampPanelSize(geometry, viewport);
  const position = clampPanelPosition(geometry, size, viewport);
  return { x: position.x, y: position.y, width: size.width, height: size.height };
}

export function samePanelGeometry(a: PanelGeometry, b: PanelGeometry): boolean {
  return a.x === b.x && a.y === b.y && a.width === b.width && a.height === b.height;
}

export function isPanelGeometry(value: unknown): value is PanelGeometry {
  if (typeof value !== "object" || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.x === "number" &&
    Number.isFinite(candidate.x) &&
    typeof candidate.y === "number" &&
    Number.isFinite(candidate.y) &&
    typeof candidate.width === "number" &&
    Number.isFinite(candidate.width) &&
    typeof candidate.height === "number" &&
    Number.isFinite(candidate.height)
  );
}
