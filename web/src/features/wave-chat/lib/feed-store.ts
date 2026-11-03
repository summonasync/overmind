import { compareChronology, mergeMessages } from "./message-stream.ts";
import type { WaveMessage } from "@/features/wave-chat/types";

const streams = new Map<string, WaveMessage[]>();
const drained = new Map<string, boolean>();

export function roomStream(projectId: string, room: string): WaveMessage[] {
  return streams.get(storeKey(projectId, room)) ?? [];
}

export function newestProjectMessage(projectId: string): WaveMessage | null {
  const prefix = `${projectId}:`;
  let newest: WaveMessage | null = null;
  for (const [key, messages] of streams) {
    if (!key.startsWith(prefix) || messages.length === 0) {
      continue;
    }
    const candidate = messages[messages.length - 1];
    if (newest === null || compareChronology(candidate, newest) > 0) {
      newest = candidate;
    }
  }
  return newest;
}

export function roomHistoryDrained(projectId: string, room: string): boolean {
  return drained.get(storeKey(projectId, room)) ?? false;
}

export function markRoomHistoryDrained(projectId: string, room: string): void {
  drained.set(storeKey(projectId, room), true);
}

export function clearRoomHistoryDrained(projectId: string, room: string): void {
  drained.set(storeKey(projectId, room), false);
}

export function absorbMessages(projectId: string, incoming: WaveMessage[]): void {
  const byRoom = new Map<string, WaveMessage[]>();
  for (const message of incoming) {
    const bucket = byRoom.get(message.room);
    if (bucket === undefined) {
      byRoom.set(message.room, [message]);
    } else {
      bucket.push(message);
    }
  }
  for (const [room, messages] of byRoom) {
    const key = storeKey(projectId, room);
    streams.set(key, mergeMessages(streams.get(key) ?? [], messages));
  }
}

function storeKey(projectId: string, room: string): string {
  return `${projectId}:${room}`;
}
