import type { WaveMessage } from "@/features/wave-chat/types";

export type RoomStreams = Record<string, WaveMessage[]>;

export type SeenCounts = Record<string, number>;

export function unreadCounts(streams: RoomStreams, seenCounts: SeenCounts): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const [room, messages] of Object.entries(streams)) {
    const seen = seenCounts[room] ?? 0;
    counts[room] = Math.max(0, messages.length - seen);
  }
  return counts;
}
