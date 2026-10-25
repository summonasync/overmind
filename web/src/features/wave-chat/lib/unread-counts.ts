import { instantOf } from "./instant.ts";
import type { WaveMessage } from "@/features/wave-chat/types";

export type RoomStreams = Record<string, WaveMessage[]>;

export type SeenStamps = Record<string, string>;

export function unreadCounts(streams: RoomStreams, seenStamps: SeenStamps): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const [room, messages] of Object.entries(streams)) {
    const boundary = instantOf(seenStamps[room] ?? null);
    counts[room] = messages.filter((message) => instantOf(message.createdAt) > boundary).length;
  }
  return counts;
}
