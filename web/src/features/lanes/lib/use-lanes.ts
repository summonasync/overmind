"use client";

import { useQuery } from "@tanstack/react-query";
import { lanesQueryOptions } from "@/features/lanes/queries";

export function useLanes(projectId: string) {
  const query = useQuery(lanesQueryOptions(projectId));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}
