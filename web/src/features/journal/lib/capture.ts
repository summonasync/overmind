export const CAPTURE_DRAFT_KEY = "overmind.journal.capture-draft";
export const CAPTURE_TITLE_MAX = 80;

export interface DraftStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export function deriveCaptureTitle(body: string): string {
  const firstLine = body.split(/\r?\n/, 1)[0].trim();
  // Code points, not UTF-16 units: a unit-based slice cuts an astral character in
  // half and ships the lone high surrogate as the entry title.
  return Array.from(firstLine).slice(0, CAPTURE_TITLE_MAX).join("");
}

// The draft is written as the owner types, not on close: a full-page navigation
// (this app routes with plain <a href>) tears the tree down without a closing
// event, and a crash costs nothing if the last keystroke already landed.
export function persistCaptureDraft(storage: DraftStorage | null, body: string): void {
  if (!storage) {
    return;
  }
  if (body.length === 0) {
    storage.removeItem(CAPTURE_DRAFT_KEY);
  } else {
    storage.setItem(CAPTURE_DRAFT_KEY, body);
  }
}

export function readCaptureDraft(storage: DraftStorage | null): string {
  if (!storage) {
    return "";
  }
  const raw = storage.getItem(CAPTURE_DRAFT_KEY);
  return typeof raw === "string" ? raw : "";
}

export function clearCaptureDraft(storage: DraftStorage | null): void {
  storage?.removeItem(CAPTURE_DRAFT_KEY);
}

export interface CaptureKeyProbe {
  key: string;
  code?: string;
  metaKey: boolean;
  ctrlKey: boolean;
  shiftKey: boolean;
  altKey: boolean;
  target?: unknown;
}

export function isCaptureHotkey(event: CaptureKeyProbe): boolean {
  const space = event.code === "Space" || event.key === " ";
  return space && event.shiftKey && !event.altKey && (event.metaKey || event.ctrlKey);
}

// Duck-typed on purpose: keeping the DOM out of this module is what lets the
// keystroke grammar run under `node --test`.
export function isEditableTarget(target: unknown): boolean {
  if (typeof target !== "object" || target === null) {
    return false;
  }
  const probe = target as { tagName?: unknown; isContentEditable?: unknown };
  const tag = typeof probe.tagName === "string" ? probe.tagName.toUpperCase() : "";
  if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") {
    return true;
  }
  return probe.isContentEditable === true;
}

export function isQuickCaptureKey(event: CaptureKeyProbe, pathname: string): boolean {
  if (event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) {
    return false;
  }
  if (event.key.toLowerCase() !== "n" || isEditableTarget(event.target ?? null)) {
    return false;
  }
  return pathname === "/journal" || pathname.startsWith("/journal/");
}

export function formatCaptureShortcut(userAgent: string): { open: string; save: string } {
  const apple = /Mac|iPhone|iPad|iPod/.test(userAgent);
  return apple ? { open: "⌘⇧Space", save: "⌘↵" } : { open: "Ctrl+Shift+Space", save: "Ctrl+Enter" };
}
