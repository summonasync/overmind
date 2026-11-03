"use client";

import { useEffect, useId, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  attachAgentTurn,
  createAgentConversation,
  listAgentHistory,
  stopAgentTurn,
  streamAgentReply,
} from "@/features/agent-chat/api";
import { agentChatKeys, agentHistoryQueryOptions } from "@/features/agent-chat/queries";
import { AGENT_HISTORY_PAGE_LIMIT, agentTitlePreview } from "@/features/agent-chat/lib/limits";
import {
  reportTurnAborted,
  reportTurnCompleted,
  reportTurnFailed,
  reportTurnStarted,
} from "@/features/agent-chat/lib/chat-activity-store";
import { approvalSettingsQueryOptions } from "@/features/projects";
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

  const completeTurn = (targetProjectId: string | null) => {
    if (targetProjectId === null) {
      reportTurnCompleted(false);
      return;
    }
    queryClient
      .fetchQuery(approvalSettingsQueryOptions(targetProjectId))
      .then((settings) => reportTurnCompleted(settings.effectiveDelegateApprovals))
      .catch(() => reportTurnCompleted(false));
  };

  const ensurePendingAssistant = (targetId: string) => {
    mutateHistory(targetId, (items) =>
      items.some((message) => message.id === pendingAssistantId)
        ? items
        : [...items, { id: pendingAssistantId, role: "assistant", content: "", createdAt: new Date().toISOString() }],
    );
  };

  const attachRef = useRef<AbortController | null>(null);
  const sendingRef = useRef(false);

  useEffect(() => {
    if (conversationId === null || sendingRef.current) {
      return;
    }
    const controller = new AbortController();
    attachRef.current = controller;
    attachAgentTurn(conversationId, {
      signal: controller.signal,
      onAttached: () => {
        if (!controller.signal.aborted) {
          setStreaming(true);
        }
      },
      onDelta: (delta) => {
        ensurePendingAssistant(conversationId);
        mutateHistory(conversationId, (items) =>
          items.map((message) =>
            message.id === pendingAssistantId ? { ...message, content: message.content + delta } : message,
          ),
        );
      },
      onCompleted: (messageId) => {
        if (messageId === null) {
          mutateHistory(conversationId, (items) =>
            items.filter((message) => message.id !== pendingAssistantId),
          );
        } else {
          mutateHistory(conversationId, (items) =>
            items.map((message) =>
              message.id === pendingAssistantId ? { ...message, id: messageId } : message,
            ),
          );
        }
        setStreaming(false);
        completeTurn(projectId);
        queryClient.invalidateQueries({ queryKey: agentChatKeys.history(conversationId) });
        invalidateConversations();
      },
      onFailed: (error) => {
        mutateHistory(conversationId, (items) =>
          items.filter((message) => message.id !== pendingAssistantId),
        );
        setStreaming(false);
        setSendError(new Error(error));
        reportTurnFailed();
        queryClient.invalidateQueries({ queryKey: agentChatKeys.history(conversationId) });
      },
      onAborted: () => {
        mutateHistory(conversationId, (items) =>
          items.filter((message) => message.id !== pendingAssistantId),
        );
        setStreaming(false);
        reportTurnAborted();
        queryClient.invalidateQueries({ queryKey: agentChatKeys.history(conversationId) });
      },
    })
      .catch((error) => {
        if (error instanceof DOMException && error.name === "AbortError") {
          return;
        }
        mutateHistory(conversationId, (items) =>
          items.filter((message) => message.id !== pendingAssistantId),
        );
        setStreaming(false);
      })
      .finally(() => {
        if (attachRef.current === controller) {
          attachRef.current = null;
        }
      });
    return () => {
      controller.abort();
      if (attachRef.current === controller) {
        attachRef.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversationId]);

  const send = async (text: string) => {
    const trimmed = text.trim();
    if (trimmed.length === 0 || streaming) {
      return;
    }
    setSendError(null);
    sendingRef.current = true;

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
      reportTurnStarted();
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
          completeTurn(projectId);
        },
        onFailed: (error) => {
          mutateHistory(activeId, (items) =>
            items.filter((message) => message.id !== pendingAssistantId),
          );
          setSendError(new Error(error));
          reportTurnFailed();
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
        reportTurnAborted();
        if (targetId !== null) {
          queryClient.invalidateQueries({ queryKey: agentChatKeys.history(targetId) });
          invalidateConversations();
        }
      } else {
        setSendError(error instanceof Error ? error : new Error(String(error)));
        reportTurnFailed();
      }
    } finally {
      abortRef.current = null;
      sendingRef.current = false;
      setStreaming(false);
    }
  };

  const stop = () => {
    abortRef.current?.abort();
    attachRef.current?.abort();
    if (conversationId !== null) {
      void stopAgentTurn(conversationId).catch(() => undefined);
    }
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
