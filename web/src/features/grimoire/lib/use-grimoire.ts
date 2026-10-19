"use client";

import { useQuery } from "@tanstack/react-query";
import { grimoireStatusQueryOptions } from "@/features/grimoire/queries";

export function useGrimoireStatus() {
  const query = useQuery(grimoireStatusQueryOptions());
  return { data: query.data ?? null, loading: query.isPending, error: query.error, refetch: query.refetch };
}
