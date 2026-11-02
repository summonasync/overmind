"use client";

import { useEffect, useMemo, type RefObject } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { markAgentConversationRead } from "@/features/agent-chat/api";
import { agentChatKeys } from "@/features/agent-chat/queries";
import { seenTracker } from "@/features/agent-chat/lib/seen-tracker";
import type { AgentMessage } from "@/features/agent-chat/types";

export function useSeenMessageTracking(
  conversationId: string | null,
  messages: AgentMessage[],
  containerRef: RefObject<HTMLElement | null>,
) {
  const queryClient = useQueryClient();
  const orderedIds = useMemo(() => messages.map((message) => message.id), [messages]);

  useEffect(() => {
    seenTracker.setFlusher(async (entries) => {
      await Promise.all(
        entries.map((entry) =>
          markAgentConversationRead(entry.conversationId, entry.lastReadMessageId),
        ),
      );
      queryClient.invalidateQueries({ queryKey: agentChatKeys.unread() });
    });
  }, [queryClient]);

  useEffect(() => {
    const container = containerRef.current;
    if (container === null || conversationId === null) {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        let seen = false;
        for (const entry of entries) {
          if (!entry.isIntersecting) {
            continue;
          }
          const messageId = (entry.target as HTMLElement).dataset.agentMessageId;
          if (messageId === undefined) {
            continue;
          }
          seenTracker.markSeen(conversationId, messageId);
          seen = true;
        }
        if (seen) {
          seenTracker.observeCandidates(conversationId, orderedIds);
        }
      },
      { root: container, threshold: 0.6 },
    );
    for (const row of container.querySelectorAll<HTMLElement>("[data-agent-message-id]")) {
      observer.observe(row);
    }
    return () => observer.disconnect();
  }, [conversationId, orderedIds, containerRef]);

  useEffect(() => {
    return () => {
      if (conversationId !== null) {
        seenTracker.flushNow();
      }
    };
  }, [conversationId]);
}
