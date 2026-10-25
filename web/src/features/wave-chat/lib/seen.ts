import { readJson, writeJson } from "../../../shared/lib/session-json.ts";

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

function seenKey(projectId: string): string {
  return `${SEEN_KEY_PREFIX}${projectId}`;
}

function isSeenMap(value: unknown): value is Record<string, string> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    return false;
  }
  return Object.values(value).every((entry) => typeof entry === "string");
}
