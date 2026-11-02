"use client";

import { useQuery } from "@tanstack/react-query";
import { agentUnreadCountQueryOptions } from "@/features/agent-chat/queries";

export function useAgentUnreadCount(): number {
  const query = useQuery(agentUnreadCountQueryOptions());
  return query.data ?? 0;
}
