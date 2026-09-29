"use client";

import { useQuery } from "@tanstack/react-query";
import { groupCeremoniesByProject } from "@/features/attention/lib/ceremony-groups";
import { ceremoniesQueryOptions } from "@/features/attention/queries";

export function useCeremonies() {
  const query = useQuery(ceremoniesQueryOptions());
  return {
    groups: groupCeremoniesByProject(query.data?.features ?? []),
    isPending: query.isPending,
    error: query.error,
  };
}
