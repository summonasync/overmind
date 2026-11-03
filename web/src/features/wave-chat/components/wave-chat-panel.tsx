"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueries, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { listWaveHistory } from "@/features/wave-chat/api";
import { WaveComposer } from "@/features/wave-chat/components/wave-composer";
import { WaveMessageList } from "@/features/wave-chat/components/wave-message-list";
import { WaveRoomHeader } from "@/features/wave-chat/components/wave-room-header";
import { WaveRoomList } from "@/features/wave-chat/components/wave-room-list";
import { WaveRoster } from "@/features/wave-chat/components/wave-roster";
import { WaveStreamChip } from "@/features/wave-chat/components/wave-stream-chip";
import {
  absorbMessages,
  clearRoomHistoryDrained,
  markRoomHistoryDrained,
  roomHistoryDrained,
} from "@/features/wave-chat/lib/feed-store";
import {
  HISTORY_PAGE_LIMIT,
  HISTORY_WINDOW_MAX,
  type UnreadWindow,
} from "@/features/wave-chat/lib/history-window";
import { DEFAULT_OWNER_ALIAS, loadOwnerAlias, saveOwnerAlias } from "@/features/wave-chat/lib/owner-alias";
import { ALL_ROOM } from "@/features/wave-chat/lib/room-order";
import { loadSeenMap, markSeen, subscribeSeenMap } from "@/features/wave-chat/lib/seen";
import {
  unreadCounts,
  type RoomStreams,
  type SeenStamps,
} from "@/features/wave-chat/lib/unread-counts";
import {
  useWaveMessageMutations,
  useWaveParticipants,
  useWaveRooms,
} from "@/features/wave-chat/lib/use-wave-chat";
import { useWaveStream } from "@/features/wave-chat/lib/use-wave-stream";
import { useWaveStreamStatus } from "@/features/wave-chat/lib/wave-stream-state";
import { syncFeedCache, waveFeedQueryOptions } from "@/features/wave-chat/queries";

const PANEL_DESCRIPTION =
  "Live wave coordination rooms. One room per lane plus the project-wide hall — post a signal, watch unread badges, address an agent with @alias.";

interface UnreadAnchor {
  room: string;
  from: string | null;
  at: string | null;
}

interface WaveChatPanelProps {
  hint?: ReactNode;
}

export function WaveChatPanel({ hint }: WaveChatPanelProps) {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";
  const [selectedRoom, setSelectedRoom] = useState<string | null>(null);
  const [ownerAlias, setOwnerAlias] = useState(DEFAULT_OWNER_ALIAS);
  const [seenReady, setSeenReady] = useState(false);
  const [seen, setSeen] = useState<SeenStamps>({});
  const [anchor, setAnchor] = useState<UnreadAnchor>({ room: "", from: null, at: null });
  const [expandedRooms, setExpandedRooms] = useState<Record<string, boolean>>({});
  const [paging, setPaging] = useState(false);
  const [pageError, setPageError] = useState<Error | null>(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setOwnerAlias(loadOwnerAlias());
      setSeen(loadSeenMap(projectId));
      setSeenReady(true);
    });
    const unsubscribe = subscribeSeenMap(projectId, setSeen);
    return () => {
      cancelAnimationFrame(frame);
      unsubscribe();
    };
  }, [projectId]);

  const board = useWaveRooms(projectId);
  const roster = useWaveParticipants(projectId);
  const { post } = useWaveMessageMutations(projectId);

  const streamStatus = useWaveStreamStatus();
  const feedOptions = useMemo(
    () =>
      board.rooms.map((room) =>
        waveFeedQueryOptions(
          projectId,
          room.room,
          seen[room.room] ?? null,
          seenReady,
          streamStatus === "open",
        ),
      ),
    [board.rooms, projectId, seen, seenReady, streamStatus],
  );
  const feeds = useQueries({ queries: feedOptions });
  useWaveStream(projectId, feeds.some((feed) => feed.isSuccess));

  const streams: RoomStreams = {};
  board.rooms.forEach((room, index) => {
    const feed = feeds[index];
    if (feed?.data) {
      streams[room.room] = feed.data;
    }
  });

  const activeRoom = selectedRoom ?? board.rooms[0]?.room ?? ALL_ROOM;
  const activeStream = streams[activeRoom] ?? [];
  const newestMessage = activeStream[activeStream.length - 1];
  const badgeCounts = { ...unreadCounts(streams, seen), [activeRoom]: 0 };

  if (seenReady && (anchor.room !== activeRoom || (anchor.at === null && newestMessage !== undefined))) {
    setAnchor({
      room: activeRoom,
      from: seen[activeRoom] ?? null,
      at: newestMessage?.createdAt ?? null,
    });
  }

  useEffect(() => {
    if (!seenReady || newestMessage === undefined) {
      return;
    }
    markSeen(projectId, activeRoom, newestMessage.createdAt);
  }, [projectId, activeRoom, seenReady, newestMessage]);

  const openRoom = (room: string) => {
    if (room === activeRoom) {
      return;
    }
    const leaving = activeStream[activeStream.length - 1];
    if (leaving !== undefined) {
      markSeen(projectId, activeRoom, leaving.createdAt);
      setSeen((current) => ({ ...current, [activeRoom]: leaving.createdAt }));
    }
    setSelectedRoom(room);
  };

  const markActiveRoomSeen = (at: string) => {
    markSeen(projectId, activeRoom, at);
    setSeen((current) => ({ ...current, [activeRoom]: at }));
  };

  const changeAlias = (alias: string) => {
    setOwnerAlias(alias);
    saveOwnerAlias(alias);
  };

  const showMore = async () => {
    const oldest = activeStream[0];
    if (oldest === undefined || paging) {
      return;
    }
    setPaging(true);
    try {
      const page = await listWaveHistory(projectId, activeRoom, {
        limit: HISTORY_PAGE_LIMIT,
        before: oldest.createdAt,
      });
      if (page.items.length < HISTORY_PAGE_LIMIT) {
        markRoomHistoryDrained(projectId, activeRoom);
      } else {
        clearRoomHistoryDrained(projectId, activeRoom);
      }
      absorbMessages(projectId, page.items);
      setExpandedRooms((current) => ({ ...current, [activeRoom]: true }));
      syncFeedCache(queryClient, projectId, activeRoom);
    } catch (error) {
      setPageError(error instanceof Error ? error : new Error(String(error)));
    } finally {
      setPaging(false);
    }
  };

  const showAll = async () => {
    if (paging) {
      return;
    }
    setPaging(true);
    try {
      const page = await listWaveHistory(projectId, activeRoom, { limit: HISTORY_WINDOW_MAX });
      markRoomHistoryDrained(projectId, activeRoom);
      absorbMessages(projectId, page.items);
      setExpandedRooms((current) => ({ ...current, [activeRoom]: true }));
      syncFeedCache(queryClient, projectId, activeRoom);
    } catch (error) {
      setPageError(error instanceof Error ? error : new Error(String(error)));
    } finally {
      setPaging(false);
    }
  };

  const unreadWindow: Pick<UnreadWindow, "from" | "to"> = {
    from: anchor.room === activeRoom ? anchor.from : (seen[activeRoom] ?? null),
    to: anchor.room === activeRoom ? anchor.at : null,
  };

  const [onlyUnread, setOnlyUnread] = useState(false);

  if (projectId === "") {
    return (
      <div>
        <PageHeader title="Wave chat" description={PANEL_DESCRIPTION} />
        <EmptyState title="Select a project first" hint="Wave rooms are scoped to one project." />
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="Wave chat"
        description={PANEL_DESCRIPTION}
        actions={<WaveStreamChip />}
      />
      <ErrorText error={board.error ?? pageError} />
      {board.loading ? (
        <LoadingState label="Loading rooms…" />
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-[220px_minmax(0,1fr)]">
          <div className="flex min-w-0 flex-col gap-3">
            <WaveRoster
              participants={roster.participants}
              loading={roster.loading}
              error={roster.error}
            />
            <WaveRoomList
              rooms={board.rooms}
              activeRoom={activeRoom}
              counts={badgeCounts}
              onOpen={openRoom}
            />
          </div>
          <section aria-label={`Messages in ${activeRoom}`} className="flex min-w-0 flex-col gap-3">
            <WaveRoomHeader room={activeRoom} participants={roster.participants} />
            <div className="flex justify-end">
              <Button
                variant="ghost"
                className="text-xs"
                aria-pressed={onlyUnread}
                onClick={() => setOnlyUnread((current) => !current)}
              >
                {onlyUnread ? "Showing unread only" : "Show only unread"}
              </Button>
            </div>
            <WaveMessageList
              messages={activeStream}
              window={unreadWindow}
              ownerAlias={ownerAlias}
              canPageBack={activeStream.length > 0 && !roomHistoryDrained(projectId, activeRoom)}
              paging={paging}
              expanded={expandedRooms[activeRoom] === true || roomHistoryDrained(projectId, activeRoom)}
              onlyUnread={onlyUnread}
              onShowMore={showMore}
              onShowAll={showAll}
              onMarkSeen={markActiveRoomSeen}
              scrollKey={activeRoom}
            />
            {hint}
            <WaveComposer
              room={activeRoom}
              ownerAlias={ownerAlias}
              onAliasChange={changeAlias}
              post={post}
            />
          </section>
        </div>
      )}
    </div>
  );
}
