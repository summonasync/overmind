"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { deleteAgentConversation } from "@/features/agent-chat/api";
import { agentChatKeys, agentConversationsQueryOptions } from "@/features/agent-chat/queries";

interface UseAgentConversationListArgs {
  projectId: string | null;
  onRemoved?: (conversationId: string) => void;
}

export function useAgentConversationList({ projectId, onRemoved }: UseAgentConversationListArgs) {
  const queryClient = useQueryClient();
  const [removeError, setRemoveError] = useState<Error | null>(null);
  const conversationsQuery = useQuery(agentConversationsQueryOptions(projectId));

  const remove = async (conversationId: string) => {
    setRemoveError(null);
    try {
      await deleteAgentConversation(conversationId);
      onRemoved?.(conversationId);
      queryClient.invalidateQueries({ queryKey: agentChatKeys.conversations(projectId) });
    } catch (error) {
      setRemoveError(error instanceof Error ? error : new Error(String(error)));
    }
  };

  return {
    conversations: conversationsQuery.data?.items ?? [],
    isPending: conversationsQuery.isPending,
    error: conversationsQuery.error ?? removeError,
    clearError: () => setRemoveError(null),
    remove,
  };
}
