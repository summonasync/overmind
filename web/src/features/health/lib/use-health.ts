"use client";

import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/features/health/queries";

export function useHealth() {
  const query = useQuery(healthQueryOptions());
  const vectorSearch = query.data?.vectorSearch ?? null;
  return {
    data: query.data ?? null,
    vectorSearch,
    vectorSearchDown: vectorSearch !== null && !vectorSearch.up,
    loading: query.isPending,
  };
}
