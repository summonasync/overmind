import type { AgentChatActivity } from "@/features/agent-chat/types";

export type BubblePose = "sleeping" | "yelling" | "surprised" | "smug" | "confused";

export const SMUG_HOLD_MS = 10_000;

export interface BubblePoseInput {
  open: boolean;
  unreadCount: number;
  activity: AgentChatActivity;
  nowMs: number;
}

export function isSmugActive(activity: AgentChatActivity, nowMs: number): boolean {
  return (
    activity.lastTurn.kind === "completed" &&
    activity.lastTurn.delegated &&
    nowMs - activity.lastTurn.completedAtMs < SMUG_HOLD_MS
  );
}

export function resolveBubblePose(input: BubblePoseInput): BubblePose {
  if (input.open && input.activity.streaming) {
    return "surprised";
  }
  if (input.activity.lastTurn.kind === "failed") {
    return "confused";
  }
  if (isSmugActive(input.activity, input.nowMs)) {
    return "smug";
  }
  if (!input.open && input.unreadCount > 0) {
    return "yelling";
  }
  return "sleeping";
}
