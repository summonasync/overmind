"use client";

import { useEffect, useId, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createAgentConversation,
  listAgentHistory,
  streamAgentReply,
} from "@/features/agent-chat/api";
import { agentChatKeys, agentHistoryQueryOptions } from "@/features/agent-chat/queries";
import { AGENT_HISTORY_PAGE_LIMIT, agentTitlePreview } from "@/features/agent-chat/lib/limits";
import type { AgentConversation, AgentMessage, AgentHistoryPayload } from "@/features/agent-chat/types";

interface UseAgentChatArgs {
  conversationId: string | null;
  projectId: string | null;
  onConversationCreated: (conversation: AgentConversation) => void;
}

export function useAgentChat({ conversationId, projectId, onConversationCreated }: UseAgentChatArgs) {
  const queryClient = useQueryClient();
  const pendingAssistantId = `pending-assistant${useId()}`;
  const [streaming, setStreaming] = useState(false);
  const [sendError, setSendError] = useState<Error | null>(null);
  const [historyDrained, setHistoryDrained] = useState(false);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const [syncedId, setSyncedId] = useState(conversationId);

  if (syncedId !== conversationId) {
    setSyncedId(conversationId);
    setSendError(null);
    setHistoryDrained(false);
  }

  useEffect(() => () => abortRef.current?.abort(), []);

  const history = useQuery(agentHistoryQueryOptions(conversationId ?? ""));
  const messages: AgentMessage[] = history.data?.items ?? [];

  const mutateHistory = (targetId: string, update: (items: AgentMessage[]) => AgentMessage[]) => {
    queryClient.setQueryData<AgentHistoryPayload>(agentChatKeys.history(targetId), (current) => ({
      items: update(current?.items ?? []),
    }));
  };

  const invalidateConversations = () => {
    queryClient.invalidateQueries({ queryKey: agentChatKeys.conversations(projectId) });
  };

  const send = async (text: string) => {
    const trimmed = text.trim();
    if (trimmed.length === 0 || streaming) {
      return;
    }
    setSendError(null);

    const controller = new AbortController();
    abortRef.current = controller;
    let targetId = conversationId;
    try {
      if (targetId === null) {
        const created = await createAgentConversation(projectId, agentTitlePreview(trimmed));
        targetId = created.id;
        onConversationCreated(created);
      }
      const activeId = targetId;
      const now = new Date().toISOString();
      mutateHistory(activeId, (items) => [
        ...items,
        { id: `pending-user-${now}`, role: "user", content: trimmed, createdAt: now },
        { id: pendingAssistantId, role: "assistant", content: "", createdAt: now },
      ]);
      setStreaming(true);
      await streamAgentReply(activeId, trimmed, {
        signal: controller.signal,
        onDelta: (delta) =>
          mutateHistory(activeId, (items) =>
            items.map((message) =>
              message.id === pendingAssistantId
                ? { ...message, content: message.content + delta }
                : message,
            ),
          ),
        onCompleted: (messageId) => {
          mutateHistory(activeId, (items) =>
            messageId === null
              ? items.filter((message) => message.id !== pendingAssistantId)
              : items.map((message) =>
                  message.id === pendingAssistantId ? { ...message, id: messageId } : message,
                ),
          );
        },
        onFailed: (error) => {
          mutateHistory(activeId, (items) =>
            items.filter((message) => message.id !== pendingAssistantId),
          );
          setSendError(new Error(error));
        },
      });
      queryClient.invalidateQueries({ queryKey: agentChatKeys.history(activeId) });
      invalidateConversations();
    } catch (error) {
      if (targetId !== null) {
        mutateHistory(targetId, (items) =>
          items.filter((message) => message.id !== pendingAssistantId),
        );
      }
      if (error instanceof DOMException && error.name === "AbortError") {
        if (targetId !== null) {
          queryClient.invalidateQueries({ queryKey: agentChatKeys.history(targetId) });
          invalidateConversations();
        }
      } else {
        setSendError(error instanceof Error ? error : new Error(String(error)));
      }
    } finally {
      abortRef.current = null;
      setStreaming(false);
    }
  };

  const stop = () => {
    abortRef.current?.abort();
  };

  const loadOlder = async () => {
    const targetId = conversationId;
    const oldest = messages[0];
    if (targetId === null || oldest === undefined || loadingOlder || streaming) {
      return;
    }
    setLoadingOlder(true);
    try {
      const page = await listAgentHistory(targetId, {
        limit: AGENT_HISTORY_PAGE_LIMIT,
        before: oldest.createdAt,
      });
      setHistoryDrained(page.items.length < AGENT_HISTORY_PAGE_LIMIT);
      if (page.items.length > 0) {
        mutateHistory(targetId, (items) => [...page.items, ...items]);
      }
    } catch (error) {
      setSendError(error instanceof Error ? error : new Error(String(error)));
    } finally {
      setLoadingOlder(false);
    }
  };

  return {
    messages,
    historyLoading: history.isPending,
    historyError: history.error,
    streaming,
    sendError,
    canLoadOlder: !historyDrained && !streaming && messages.length >= AGENT_HISTORY_PAGE_LIMIT,
    loadingOlder,
    send,
    stop,
    loadOlder,
  };
}
