import { instantOf } from "./instant.ts";
import type { WaveRoom } from "@/features/wave-chat/types";

export const ALL_ROOM = "all";

export function sortRooms(rooms: WaveRoom[]): WaveRoom[] {
  return [...rooms].sort((left, right) => {
    const byRank = roomRank(left.room) - roomRank(right.room);
    if (byRank !== 0) {
      return byRank;
    }
    const byActivity = instantOf(right.lastActivityAt) - instantOf(left.lastActivityAt);
    if (byActivity !== 0) {
      return byActivity;
    }
    return left.room.localeCompare(right.room);
  });
}

function roomRank(room: string): number {
  return room === ALL_ROOM ? 0 : 1;
}
