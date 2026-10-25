export type WaveRole = "owner" | "worker" | "marshal";

export interface WaveRoom {
  room: string;
  lastActivityAt: string | null;
}

export interface WaveMessage {
  id: string;
  room: string;
  authorAlias: string;
  authorRole: WaveRole;
  text: string;
  mentions: string[];
  createdAt: string;
}

export interface WaveRoomsPayload {
  items: WaveRoom[];
}

export interface WaveHistoryPayload {
  items: WaveMessage[];
}

export type WaveParticipantKind = "owner" | "agent";

export type WaveClaimState = "alive" | "stale" | "ended";

export interface WaveParticipant {
  kind: WaveParticipantKind;
  lane: string | null;
  alias: string;
  vendor: string | null;
  claimState: WaveClaimState | null;
  role: WaveRole;
}

export interface WaveParticipantsPayload {
  items: WaveParticipant[];
}
