export interface WaveRoom {
  room: string;
  lastActivityAt: string | null;
}

export interface WaveMessage {
  id: string;
  room: string;
  authorAlias: string;
  text: string;
  mentions: string[];
  createdAt: string;
}

export interface WaveRoomsPayload {
  items: WaveRoom[];
}

export interface WaveUnreadPayload {
  items: WaveMessage[];
  watermarkAt: string;
}
