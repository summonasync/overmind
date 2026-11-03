import { readJson, writeJson } from "../../../shared/lib/local-json.ts";

const SEEN_KEY_PREFIX = "overmind.wave-chat.seen.";

export function loadSeenMap(projectId: string): Record<string, string> {
  const stored = readJson<unknown>(seenKey(projectId), {});
  return isSeenMap(stored) ? stored : {};
}

export function loadLastSeen(projectId: string, room: string): string | null {
  const at = loadSeenMap(projectId)[room];
  return typeof at === "string" ? at : null;
}

export function markSeen(projectId: string, room: string, at: string): void {
  const seen = loadSeenMap(projectId);
  const current = seen[room];
  if (current === at) {
    return;
  }
  writeJson(seenKey(projectId), { ...seen, [room]: at });
}

export function subscribeSeenMap(
  projectId: string,
  listener: (seen: Record<string, string>) => void,
): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }
  const key = seenKey(projectId);
  const onStorage = (event: StorageEvent) => {
    if (event.key !== key && event.key !== null) {
      return;
    }
    listener(parseSeenMap(event.newValue));
  };
  window.addEventListener("storage", onStorage);
  return () => window.removeEventListener("storage", onStorage);
}

function seenKey(projectId: string): string {
  return `${SEEN_KEY_PREFIX}${projectId}`;
}

function parseSeenMap(raw: string | null): Record<string, string> {
  if (raw === null) {
    return {};
  }
  try {
    const parsed: unknown = JSON.parse(raw);
    return isSeenMap(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function isSeenMap(value: unknown): value is Record<string, string> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    return false;
  }
  return Object.values(value).every((entry) => typeof entry === "string");
}
