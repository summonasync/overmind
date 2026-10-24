import { queryOptions, type QueryClient } from "@tanstack/react-query";
import { listWaveParticipants, listWaveRooms, listWaveUnread } from "@/features/wave-chat/api";
import { absorbMessages, roomStream } from "@/features/wave-chat/lib/feed-store";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const waveChatKeys = {
  all: ["wave-chat"] as const,
  rooms: (projectId: string) => [...waveChatKeys.all, "rooms", projectId] as const,
  participants: (projectId: string) => [...waveChatKeys.all, "participants", projectId] as const,
  feed: (projectId: string, alias: string, room: string) =>
    [...waveChatKeys.all, "feed", projectId, alias, room] as const,
};

export const waveRoomsQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: waveChatKeys.rooms(projectId),
    queryFn: () => listWaveRooms(projectId),
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const waveParticipantsQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: waveChatKeys.participants(projectId),
    queryFn: () => listWaveParticipants(projectId),
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export function waveFeedQueryOptions(
  queryClient: QueryClient,
  projectId: string,
  alias: string,
  room: string,
) {
  return queryOptions({
    queryKey: waveChatKeys.feed(projectId, alias, room),
    queryFn: async () => {
      const page = await listWaveUnread(projectId, alias, room);
      absorbMessages(projectId, page.items);
      return roomStream(projectId, room);
    },
    enabled: alias.trim().length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
}
