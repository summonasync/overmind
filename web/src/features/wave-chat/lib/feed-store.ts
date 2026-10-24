import { mergeMessages } from "./message-stream.ts";
import type { WaveMessage } from "@/features/wave-chat/types";

const streams = new Map<string, WaveMessage[]>();

export function roomStream(projectId: string, room: string): WaveMessage[] {
  return streams.get(storeKey(projectId, room)) ?? [];
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
