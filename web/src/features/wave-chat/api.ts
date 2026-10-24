import { apiGet, apiPost } from "@/shared/api/client";
import type { WaveMessage, WaveRoomsPayload, WaveUnreadPayload } from "@/features/wave-chat/types";

export function listWaveRooms(projectId: string): Promise<WaveRoomsPayload> {
  return apiGet<WaveRoomsPayload>(`/projects/${projectId}/wave/rooms`);
}

export function listWaveUnread(projectId: string, alias: string, room: string): Promise<WaveUnreadPayload> {
  const query = new URLSearchParams({ alias });
  return apiGet<WaveUnreadPayload>(
    `/projects/${projectId}/wave/rooms/${encodeURIComponent(room)}/unread?${query}`,
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
