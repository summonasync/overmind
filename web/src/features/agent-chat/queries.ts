import { queryOptions } from "@tanstack/react-query";
import { listAgentConversations, listAgentHistory } from "@/features/agent-chat/api";
import { AGENT_HISTORY_PAGE_LIMIT } from "@/features/agent-chat/lib/limits";

export const agentChatKeys = {
  all: ["agent-chat"] as const,
  conversations: (projectId: string | null) =>
    [...agentChatKeys.all, "conversations", projectId ?? "none"] as const,
  history: (conversationId: string) => [...agentChatKeys.all, "history", conversationId] as const,
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
