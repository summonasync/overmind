"use client";

import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/features/health/queries";

export function useHealth() {
  const query = useQuery(healthQueryOptions());
  return {
    data: query.data ?? null,
    vectorSearchDown: query.data ? query.data.vectorSearch === "down" : false,
    loading: query.isPending,
  };
}
