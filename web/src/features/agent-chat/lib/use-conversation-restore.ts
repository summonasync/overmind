"use client";

import { useEffect, useRef } from "react";
import {
  readSelectedConversationId,
  writeSelectedConversationId,
} from "@/features/agent-chat/lib/selected-conversation";
import type { AgentConversation } from "@/features/agent-chat/types";

interface UseConversationRestoreArgs {
  conversations: AgentConversation[];
  isPending: boolean;
  selectedId: string | null;
  select: (conversationId: string | null) => void;
}

export function useConversationRestore({
  conversations,
  isPending,
  selectedId,
  select,
}: UseConversationRestoreArgs) {
  const restoredRef = useRef(false);

  useEffect(() => {
    if (isPending || restoredRef.current) {
      return;
    }
    restoredRef.current = true;
    const storedId = readSelectedConversationId();
    if (storedId === null) {
      return;
    }
    const exists = conversations.some((conversation) => conversation.id === storedId);
    select(exists ? storedId : (conversations[0]?.id ?? null));
  }, [conversations, isPending, select]);

  useEffect(() => {
    if (isPending || selectedId === null || conversations.some((c) => c.id === selectedId)) {
      return;
    }
    select(conversations[0]?.id ?? null);
  }, [conversations, isPending, selectedId, select]);

  useEffect(() => {
    if (!restoredRef.current) {
      return;
    }
    writeSelectedConversationId(selectedId);
  }, [selectedId]);
}
