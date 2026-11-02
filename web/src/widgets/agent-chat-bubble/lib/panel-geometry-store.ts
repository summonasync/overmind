import { useSyncExternalStore } from "react";
import {
  clampPanelGeometry,
  isPanelGeometry,
  readViewportSize,
  samePanelGeometry,
  type PanelGeometry,
} from "@/widgets/agent-chat-bubble/lib/panel-geometry";

const STORAGE_KEY = "overmind.agent-chat.panel-geometry";
const STORAGE_VERSION = 1;

interface PanelGeometryState {
  geometry: PanelGeometry | null;
  maximized: boolean;
}

interface StoredPanelGeometry extends PanelGeometryState {
  version: number;
}

const DEFAULT_STATE: PanelGeometryState = { geometry: null, maximized: false };

let state = DEFAULT_STATE;
let loaded = false;
const listeners = new Set<() => void>();

function parseStored(raw: string): PanelGeometryState | null {
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== "object" || parsed === null) {
      return null;
    }
    const stored = parsed as Partial<StoredPanelGeometry>;
    if (stored.version !== STORAGE_VERSION) {
      return null;
    }
    return {
      geometry: isPanelGeometry(stored.geometry) ? stored.geometry : null,
      maximized: stored.maximized === true,
    };
  } catch {
    return null;
  }
}

function loadState(): PanelGeometryState {
  if (typeof window === "undefined") {
    return DEFAULT_STATE;
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw === null) {
      return DEFAULT_STATE;
    }
    const stored = parseStored(raw);
    if (stored === null) {
      return DEFAULT_STATE;
    }
    return {
      geometry: stored.geometry === null ? null : clampPanelGeometry(stored.geometry, readViewportSize()),
      maximized: stored.maximized,
    };
  } catch {
    return DEFAULT_STATE;
  }
}

function ensureLoaded(): void {
  if (loaded) {
    return;
  }
  loaded = true;
  state = loadState();
}

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

function setState(next: PanelGeometryState): void {
  state = next;
  emit();
}

function persist(): void {
  const stored: StoredPanelGeometry = {
    version: STORAGE_VERSION,
    geometry: state.geometry,
    maximized: state.maximized,
  };
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
  } catch {
    return;
  }
}

export function subscribePanelGeometry(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function panelGeometrySnapshot(): PanelGeometryState {
  ensureLoaded();
  return state;
}

export function usePanelGeometryState(): PanelGeometryState {
  return useSyncExternalStore(subscribePanelGeometry, panelGeometrySnapshot, () => DEFAULT_STATE);
}

export function setPanelGeometry(geometry: PanelGeometry): void {
  setState({ ...state, geometry });
}

export function commitPanelGeometry(): void {
  persist();
}

export function setPanelMaximized(maximized: boolean): void {
  if (state.maximized === maximized) {
    return;
  }
  setState({ ...state, maximized });
  persist();
}

export function ensurePanelGeometryFromRect(rect: DOMRect): PanelGeometry {
  if (state.geometry !== null) {
    return state.geometry;
  }
  const geometry = clampPanelGeometry(
    { x: rect.left, y: rect.top, width: rect.width, height: rect.height },
    readViewportSize(),
  );
  setState({ ...state, geometry });
  return geometry;
}

export function clampPanelGeometryToViewport(): void {
  if (state.geometry === null) {
    return;
  }
  const clamped = clampPanelGeometry(state.geometry, readViewportSize());
  if (samePanelGeometry(clamped, state.geometry)) {
    return;
  }
  setState({ ...state, geometry: clamped });
  persist();
}
