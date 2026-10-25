import { instantOf } from "./instant.ts";
import type { WaveMessage } from "@/features/wave-chat/types";

export const HISTORY_PAGE_LIMIT = 20;
export const HISTORY_WINDOW_MIN = 20;
export const HISTORY_WINDOW_MAX = 500;

export interface UnreadWindow {
  room: string;
  from: string | null;
  to: string | null;
}

export interface UnreadSlices {
  read: WaveMessage[];
  unread: WaveMessage[];
  live: WaveMessage[];
}

export function historyWindowSize(unreadCount: number): number {
  return Math.min(HISTORY_WINDOW_MAX, Math.max(HISTORY_WINDOW_MIN, unreadCount + 5));
}

export function countUnread(messages: WaveMessage[], boundaryMs: number): number {
  return messages.filter((message) => instantOf(message.createdAt) > boundaryMs).length;
}

export function unreadExceedsWindow(
  page: WaveMessage[],
  requested: number,
  boundaryMs: number,
): boolean {
  if (page.length < requested) {
    return false;
  }
  const oldest = page[0];
  return oldest !== undefined && instantOf(oldest.createdAt) > boundaryMs;
}

export function sliceByUnread(
  stream: WaveMessage[],
  window: Pick<UnreadWindow, "from" | "to">,
): UnreadSlices {
  const from = instantOf(window.from);
  const to = instantOf(window.to);
  const slices: UnreadSlices = { read: [], unread: [], live: [] };
  for (const message of stream) {
    const at = instantOf(message.createdAt);
    if (to > 0) {
      if (at > from && at <= to) {
        slices.unread.push(message);
      } else if (at > to) {
        slices.live.push(message);
      } else {
        slices.read.push(message);
      }
    } else if (at > from) {
      slices.unread.push(message);
    } else {
      slices.read.push(message);
    }
  }
  return slices;
}
