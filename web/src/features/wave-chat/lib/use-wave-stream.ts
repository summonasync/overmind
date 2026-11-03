"use client";

import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { absorbMessages, newestProjectMessage } from "@/features/wave-chat/lib/feed-store";
import { setWaveStreamStatus } from "@/features/wave-chat/lib/wave-stream-state";
import { runWaveStream, type WaveStreamCursor } from "@/features/wave-chat/lib/wave-stream";
import { syncFeedCache, waveChatKeys } from "@/features/wave-chat/queries";

export function useWaveStream(projectId: string, snapshotReady: boolean): void {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!snapshotReady || projectId === "") {
      return;
    }
    const controller = new AbortController();
    void runWaveStream(projectId, {
      getCursor: () => cursorOf(projectId),
      onMessage: (message) => {
        absorbMessages(projectId, [message]);
        const feedKey = waveChatKeys.feed(projectId, message.room);
        if (queryClient.getQueryData(feedKey) !== undefined) {
          syncFeedCache(queryClient, projectId, message.room);
        }
      },
      onStatus: (reconnecting) => setWaveStreamStatus(reconnecting ? "reconnecting" : "open"),
    }, controller.signal);
    return () => {
      controller.abort();
      setWaveStreamStatus("idle");
    };
  }, [projectId, snapshotReady, queryClient]);
}

function cursorOf(projectId: string): WaveStreamCursor | null {
  const newest = newestProjectMessage(projectId);
  return newest === null ? null : { since: newest.createdAt, sinceId: newest.id };
}
