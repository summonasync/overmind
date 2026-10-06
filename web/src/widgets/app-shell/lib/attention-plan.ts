import type { NotificationKind } from "@/features/notifications";

export type DesktopPermission = "granted" | "denied" | "default" | "unsupported";

export interface AttentionEnvironment {
  hidden: boolean;
  desktopEnabled: boolean;
  chirpEnabled: boolean;
  permission: DesktopPermission;
}

export interface AttentionEvent {
  id: string;
  kind: NotificationKind;
}

export interface AttentionDecision {
  toast: boolean;
  chirp: boolean;
  raise: boolean;
}

export interface AttentionState {
  firedIds: string[];
  attentionIds: string[];
}

export const initialAttentionState: AttentionState = { firedIds: [], attentionIds: [] };

const QUIET_DECISION: AttentionDecision = { toast: false, chirp: false, raise: false };

const ATTENTION_KINDS: ReadonlySet<NotificationKind> = new Set(["approval", "question"]);

export function isAttentionKind(kind: NotificationKind): boolean {
  return ATTENTION_KINDS.has(kind);
}

export function attentionDecision(
  event: AttentionEvent,
  seen: boolean,
  env: AttentionEnvironment,
): AttentionDecision {
  if (!env.hidden || seen || !isAttentionKind(event.kind)) {
    return QUIET_DECISION;
  }
  return {
    toast: env.desktopEnabled && env.permission === "granted",
    chirp: env.chirpEnabled,
    raise: true,
  };
}

export function markSeen(state: AttentionState, id: string): AttentionState {
  return state.firedIds.includes(id) ? state : { ...state, firedIds: [...state.firedIds, id] };
}

export function markAllSeen(state: AttentionState, ids: string[]): AttentionState {
  const known = new Set(state.firedIds);
  const fresh = ids.filter((id) => !known.has(id));
  return fresh.length === 0 ? state : { ...state, firedIds: [...state.firedIds, ...fresh] };
}

export function raiseAttention(state: AttentionState, id: string): AttentionState {
  return state.attentionIds.includes(id)
    ? state
    : { ...state, attentionIds: [...state.attentionIds, id] };
}

export function clearAttention(state: AttentionState): AttentionState {
  return state.attentionIds.length === 0 ? state : { ...state, attentionIds: [] };
}

export function pruneAttention(state: AttentionState, awaitingIds: string[]): AttentionState {
  const live = new Set(awaitingIds);
  const nextAttention = state.attentionIds.filter((id) => live.has(id));
  const nextFired = state.firedIds.filter((id) => live.has(id));
  if (nextAttention.length === state.attentionIds.length && nextFired.length === state.firedIds.length) {
    return state;
  }

  return { ...state, attentionIds: nextAttention, firedIds: nextFired };
}

export function attentionCount(state: AttentionState): number {
  return state.attentionIds.length;
}
