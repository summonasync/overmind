import { queryOptions } from "@tanstack/react-query";
import {
  fetchAgentUnreadCount,
  fetchAgentUsage,
  listAgentConversations,
  listAgentHistory,
} from "@/features/agent-chat/api";
import { AGENT_HISTORY_PAGE_LIMIT } from "@/features/agent-chat/lib/limits";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const agentChatKeys = {
  all: ["agent-chat"] as const,
  conversations: (projectId: string | null) =>
    [...agentChatKeys.all, "conversations", projectId ?? "none"] as const,
  history: (conversationId: string) => [...agentChatKeys.all, "history", conversationId] as const,
  unread: () => [...agentChatKeys.all, "unread"] as const,
  usage: () => [...agentChatKeys.all, "usage"] as const,
};

export const agentConversationsQueryOptions = (projectId: string | null) =>
  queryOptions({
    queryKey: agentChatKeys.conversations(projectId),
    queryFn: () => listAgentConversations(projectId),
  });

export function agentHistoryQueryOptions(conversationId: string) {
  return queryOptions({
    queryKey: agentChatKeys.history(conversationId),
    queryFn: () => listAgentHistory(conversationId, { limit: AGENT_HISTORY_PAGE_LIMIT }),
    enabled: conversationId.length > 0,
  });
}

export const agentUnreadCountQueryOptions = () =>
  queryOptions({
    queryKey: agentChatKeys.unread(),
    queryFn: fetchAgentUnreadCount,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const agentUsageQueryOptions = () =>
  queryOptions({
    queryKey: agentChatKeys.usage(),
    queryFn: fetchAgentUsage,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
