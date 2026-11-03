"use client";

import { useQuery } from "@tanstack/react-query";
import { agentUsageQueryOptions } from "@/features/agent-chat/queries";

export function useAgentUsage() {
  return useQuery(agentUsageQueryOptions());
}
