import { apiStreamGet } from "@/shared/api/client";
import { readSseEvents } from "@/shared/lib/sse";
import type { WaveMessage, WaveRole } from "@/features/wave-chat/types";

export interface WaveStreamCursor {
  since: string;
  sinceId: string;
}

export interface WaveStreamHandlers {
  getCursor: () => WaveStreamCursor | null;
  onMessage: (message: WaveMessage) => void;
  onStatus: (reconnecting: boolean) => void;
}

const RECONNECT_BASE_DELAY_MS = 1_000;
const RECONNECT_MAX_DELAY_MS = 15_000;
const RECONNECT_RESET_AFTER_MS = 10_000;
const IDLE_WATCHDOG_MS = 60_000;

const ROLES: readonly WaveRole[] = ["owner", "worker", "marshal", "embedded"];

export async function runWaveStream(
  projectId: string,
  handlers: WaveStreamHandlers,
  signal: AbortSignal,
): Promise<void> {
  let delay = RECONNECT_BASE_DELAY_MS;
  while (!signal.aborted) {
    let openedLiveAt = 0;
    try {
      await openWaveStream(projectId, handlers, signal, () => {
        openedLiveAt = Date.now();
      });
    } catch {
      if (signal.aborted) {
        return;
      }
    }
    if (signal.aborted) {
      return;
    }
    if (openedLiveAt > 0 && Date.now() - openedLiveAt >= RECONNECT_RESET_AFTER_MS) {
      delay = RECONNECT_BASE_DELAY_MS;
    }
    handlers.onStatus(true);
    await sleep(delay, signal);
    delay = Math.min(delay * 2, RECONNECT_MAX_DELAY_MS);
  }
}

async function openWaveStream(
  projectId: string,
  handlers: WaveStreamHandlers,
  signal: AbortSignal,
  onOpen: () => void,
): Promise<void> {
  const cursor = handlers.getCursor();
  const search = new URLSearchParams();
  if (cursor !== null) {
    search.set("since", cursor.since);
    search.set("sinceId", cursor.sinceId);
  }
  const suffix = search.size > 0 ? `?${search.toString()}` : "";
  const controller = new AbortController();
  const abortWithParent = () => controller.abort();
  signal.addEventListener("abort", abortWithParent);
  let idleTimer: ReturnType<typeof setTimeout> | null = null;
  const armIdleWatchdog = () => {
    if (idleTimer !== null) {
      clearTimeout(idleTimer);
    }
    idleTimer = setTimeout(() => {
      idleTimer = null;
      controller.abort();
    }, IDLE_WATCHDOG_MS);
  };
  try {
    const response = await apiStreamGet(`/projects/${projectId}/wave/stream${suffix}`, controller.signal);
    if (!response.ok) {
      throw new Error(`The wave stream connect failed (${response.status}).`);
    }
    if (!response.body) {
      throw new Error("The wave stream returned no body.");
    }
    handlers.onStatus(false);
    onOpen();
    armIdleWatchdog();
    const body = response.body.pipeThrough(
      new TransformStream<Uint8Array, Uint8Array>({
        transform(chunk, sink) {
          armIdleWatchdog();
          sink.enqueue(chunk);
        },
      }),
    );
    await readSseEvents(body, (event) => {
      if (event.name !== "message") {
        return;
      }
      const message = parseWaveStreamFrame(event.data);
      if (message !== null) {
        handlers.onMessage(message);
      }
    });
  } finally {
    if (idleTimer !== null) {
      clearTimeout(idleTimer);
    }
    signal.removeEventListener("abort", abortWithParent);
    controller.abort();
  }
}

export function parseWaveStreamFrame(data: string): WaveMessage | null {
  let payload: unknown;
  try {
    payload = JSON.parse(data);
  } catch {
    return null;
  }
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const frame = payload as Record<string, unknown>;
  const message = frame.message;
  if (message === null || typeof message !== "object" || Array.isArray(message)) {
    return null;
  }

  const record = message as Record<string, unknown>;
  if (
    typeof record.id !== "string" ||
    typeof record.room !== "string" ||
    typeof record.authorAlias !== "string" ||
    typeof record.authorRole !== "string" ||
    !isWaveRole(record.authorRole) ||
    typeof record.text !== "string" ||
    typeof record.createdAt !== "string"
  ) {
    return null;
  }
  return {
    id: record.id,
    room: record.room,
    authorAlias: record.authorAlias,
    authorRole: record.authorRole,
    text: record.text,
    mentions: toStringList(record.mentions),
    createdAt: record.createdAt,
  };
}

function isWaveRole(value: string): value is WaveRole {
  return ROLES.some((role) => role === value);
}

function toStringList(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.filter((item): item is string => typeof item === "string");
}

function sleep(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(finish, ms);
    function finish(): void {
      clearTimeout(timer);
      signal.removeEventListener("abort", finish);
      resolve();
    }
    signal.addEventListener("abort", finish, { once: true });
  });
}
