import { instantOf } from "./instant.ts";
import type { WaveMessage } from "@/features/wave-chat/types";

export function mergeMessages(known: WaveMessage[], incoming: WaveMessage[]): WaveMessage[] {
  const byId = new Map(known.map((message) => [message.id, message]));
  for (const message of incoming) {
    byId.set(message.id, message);
  }
  return [...byId.values()].sort(compareChronology);
}

function compareChronology(left: WaveMessage, right: WaveMessage): number {
  const byTime = instantOf(left.createdAt) - instantOf(right.createdAt);
  if (byTime !== 0) {
    return byTime;
  }
  return left.id.localeCompare(right.id);
}
