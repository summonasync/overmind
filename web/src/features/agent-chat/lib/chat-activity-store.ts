"use client";

import { useSyncExternalStore } from "react";
import type { AgentChatActivity, ChatTurnOutcome } from "@/features/agent-chat/types";

let activity: AgentChatActivity = { streaming: false, lastTurn: { kind: "idle" } };
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

function apply(next: AgentChatActivity): void {
  if (activity.streaming === next.streaming && activity.lastTurn === next.lastTurn) {
    return;
  }
  activity = next;
  emit();
}

export function subscribeChatActivity(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function chatActivitySnapshot(): AgentChatActivity {
  return activity;
}

export function useChatActivity(): AgentChatActivity {
  return useSyncExternalStore(subscribeChatActivity, chatActivitySnapshot, chatActivitySnapshot);
}

export function reportTurnStarted(): void {
  apply({ streaming: true, lastTurn: { kind: "idle" } });
}

export function reportTurnCompleted(delegated: boolean): void {
  apply({
    streaming: false,
    lastTurn: { kind: "completed", delegated, completedAtMs: Date.now() },
  });
}

export function reportTurnFailed(): void {
  apply({ streaming: false, lastTurn: { kind: "failed", failedAtMs: Date.now() } });
}

export function reportTurnAborted(): void {
  apply({ streaming: false, lastTurn: { kind: "idle" } });
}

export type { ChatTurnOutcome };
