"use client";

import { useEffect, useMemo, useState } from "react";
import { useQueries, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { ErrorText } from "@/shared/ui/error-text";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { WaveComposer } from "@/features/wave-chat/components/wave-composer";
import { WaveMessageList } from "@/features/wave-chat/components/wave-message-list";
import { WaveRoomList } from "@/features/wave-chat/components/wave-room-list";
import { DEFAULT_OWNER_ALIAS, loadOwnerAlias, saveOwnerAlias } from "@/features/wave-chat/lib/owner-alias";
import { ALL_ROOM } from "@/features/wave-chat/lib/room-order";
import { unreadCounts, type RoomStreams, type SeenCounts } from "@/features/wave-chat/lib/unread-counts";
import { useWaveMessageMutations, useWaveRooms } from "@/features/wave-chat/lib/use-wave-chat";
import { waveFeedQueryOptions } from "@/features/wave-chat/queries";

const PANEL_DESCRIPTION =
  "Live wave coordination rooms. One room per lane plus the project-wide hall — post a signal, watch unread badges, address an agent with @alias.";

export function WaveChatPanel() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";
  const [selectedRoom, setSelectedRoom] = useState<string | null>(null);
  const [seenCounts, setSeenCounts] = useState<SeenCounts>({});
  const [ownerAlias, setOwnerAlias] = useState(DEFAULT_OWNER_ALIAS);

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      setOwnerAlias(loadOwnerAlias());
    });
    return () => cancelAnimationFrame(frame);
  }, []);

  const board = useWaveRooms(projectId);
  const { post } = useWaveMessageMutations(projectId);
  const queryClient = useQueryClient();

  const feedOptions = useMemo(
    () => board.rooms.map((room) => waveFeedQueryOptions(queryClient, projectId, ownerAlias, room.room)),
    [board.rooms, queryClient, projectId, ownerAlias],
  );
  const feeds = useQueries({ queries: feedOptions });

  const streams: RoomStreams = {};
  board.rooms.forEach((room, index) => {
    const feed = feeds[index];
    if (feed?.data) {
      streams[room.room] = feed.data;
    }
  });

  const activeRoom = selectedRoom ?? board.rooms[0]?.room ?? ALL_ROOM;
  const activeStream = streams[activeRoom] ?? [];
  const badgeCounts = { ...unreadCounts(streams, seenCounts), [activeRoom]: 0 };

  const openRoom = (room: string) => {
    if (room === activeRoom) {
      return;
    }
    setSeenCounts((current) => ({ ...current, [activeRoom]: activeStream.length }));
    setSelectedRoom(room);
  };

  const changeAlias = (alias: string) => {
    setOwnerAlias(alias);
    saveOwnerAlias(alias);
  };

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
      <PageHeader title="Wave chat" description={PANEL_DESCRIPTION} />
      <ErrorText error={board.error} />
      {board.loading ? (
        <LoadingState label="Loading rooms…" />
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-[220px_minmax(0,1fr)]">
          <WaveRoomList
            rooms={board.rooms}
            activeRoom={activeRoom}
            counts={badgeCounts}
            onOpen={openRoom}
          />
          <section aria-label={`Messages in ${activeRoom}`} className="flex min-w-0 flex-col gap-3">
            <WaveMessageList messages={activeStream} ownerAlias={ownerAlias} />
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
