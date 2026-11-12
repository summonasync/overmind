"use client";

import { useQuery } from "@tanstack/react-query";
import { usageSummaryQueryOptions } from "@/features/usage/queries";

export function useUsageSummary(projectId: string, since?: string, until?: string) {
  const query = useQuery(usageSummaryQueryOptions(projectId, since, until));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}
