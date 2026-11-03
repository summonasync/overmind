import { queryOptions, type QueryClient } from "@tanstack/react-query";
import { listWaveHistory, listWaveParticipants, listWaveRooms } from "@/features/wave-chat/api";
import {
  absorbMessages,
  clearRoomHistoryDrained,
  markRoomHistoryDrained,
  roomStream,
} from "@/features/wave-chat/lib/feed-store";
import {
  countUnread,
  historyWindowSize,
  HISTORY_WINDOW_MAX,
  unreadExceedsWindow,
} from "@/features/wave-chat/lib/history-window";
import { instantOf } from "@/features/wave-chat/lib/instant";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const waveChatKeys = {
  all: ["wave-chat"] as const,
  rooms: (projectId: string) => [...waveChatKeys.all, "rooms", projectId] as const,
  participants: (projectId: string) => [...waveChatKeys.all, "participants", projectId] as const,
  feed: (projectId: string, room: string) => [...waveChatKeys.all, "feed", projectId, room] as const,
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
  projectId: string,
  room: string,
  lastSeenAt: string | null,
  enabled: boolean,
  pollingPaused: boolean,
) {
  return queryOptions({
    queryKey: waveChatKeys.feed(projectId, room),
    queryFn: async () => {
      const boundary = instantOf(lastSeenAt);
      let size = historyWindowSize(countUnread(roomStream(projectId, room), boundary));
      let page = await listWaveHistory(projectId, room, { limit: size });
      while (size < HISTORY_WINDOW_MAX && unreadExceedsWindow(page.items, size, boundary)) {
        size = Math.min(HISTORY_WINDOW_MAX, size * 2);
        page = await listWaveHistory(projectId, room, { limit: size });
      }
      if (page.items.length < size) {
        markRoomHistoryDrained(projectId, room);
      } else {
        clearRoomHistoryDrained(projectId, room);
      }
      absorbMessages(projectId, page.items);
      return roomStream(projectId, room);
    },
    enabled,
    refetchInterval: pollingPaused ? false : LIVE_SYNC_INTERVAL_MS,
  });
}

export function syncFeedCache(queryClient: QueryClient, projectId: string, room: string): void {
  queryClient.setQueryData<ReturnType<typeof roomStream>>(
    waveChatKeys.feed(projectId, room),
    roomStream(projectId, room),
  );
}
