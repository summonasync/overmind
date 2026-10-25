import { apiGet, apiPost } from "@/shared/api/client";
import type {
  WaveHistoryPayload,
  WaveMessage,
  WaveParticipantsPayload,
  WaveRoomsPayload,
} from "@/features/wave-chat/types";

export interface WaveHistoryQuery {
  limit?: number;
  before?: string;
}

export function listWaveRooms(projectId: string): Promise<WaveRoomsPayload> {
  return apiGet<WaveRoomsPayload>(`/projects/${projectId}/wave/rooms`);
}

export function listWaveParticipants(projectId: string): Promise<WaveParticipantsPayload> {
  return apiGet<WaveParticipantsPayload>(`/projects/${projectId}/wave/participants`);
}

export function listWaveHistory(
  projectId: string,
  room: string,
  query: WaveHistoryQuery = {},
): Promise<WaveHistoryPayload> {
  const search = new URLSearchParams();
  if (query.limit !== undefined) {
    search.set("limit", String(query.limit));
  }
  if (query.before !== undefined) {
    search.set("before", query.before);
  }
  const queryString = search.toString();
  const suffix = queryString.length > 0 ? `?${queryString}` : "";
  return apiGet<WaveHistoryPayload>(
    `/projects/${projectId}/wave/rooms/${encodeURIComponent(room)}/messages${suffix}`,
  );
}

export function postWaveMessage(
  projectId: string,
  room: string,
  authorAlias: string,
  text: string,
): Promise<WaveMessage> {
  const payload = { authorAlias, text };
  return apiPost<WaveMessage>(
    `/projects/${projectId}/wave/rooms/${encodeURIComponent(room)}/messages`,
    payload,
  );
}
