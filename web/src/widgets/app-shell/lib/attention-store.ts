"use client";

import { useSyncExternalStore } from "react";
import {
  attentionCount,
  clearAttention,
  initialAttentionState,
  markAllSeen,
  markSeen,
  pruneAttention,
  raiseAttention,
  type AttentionState,
} from "@/widgets/app-shell/lib/attention-plan";

let state: AttentionState = initialAttentionState;
let snapshot = { count: 0 };
const listeners = new Set<() => void>();

function emit(): void {
  const count = attentionCount(state);
  if (snapshot.count === count) {
    return;
  }
  snapshot = { count };
  for (const listener of listeners) {
    listener();
  }
}

export function subscribeAttention(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function attentionSnapshot(): { count: number } {
  return snapshot;
}

export function useAttentionCount(): number {
  return useSyncExternalStore(subscribeAttention, attentionSnapshot, attentionSnapshot).count;
}

export function attentionSeen(id: string): boolean {
  return state.firedIds.includes(id);
}

export function markAttentionSeen(id: string): void {
  state = markSeen(state, id);
}

export function primeAttentionSeen(ids: string[]): void {
  state = markAllSeen(state, ids);
}

export function raiseAttentionFor(id: string): void {
  state = raiseAttention(state, id);
  emit();
}

export function clearAttentionSurfaces(): void {
  state = clearAttention(state);
  emit();
}

export function pruneAttentionAgainst(awaitingIds: string[]): void {
  state = pruneAttention(state, awaitingIds);
  emit();
}
