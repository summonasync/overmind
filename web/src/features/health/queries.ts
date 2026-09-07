import { queryOptions } from "@tanstack/react-query";
import { fetchHealth } from "@/features/health/api";

export const healthKeys = {
  all: ["health"] as const,
  status: () => [...healthKeys.all, "status"] as const,
};

export const healthQueryOptions = () =>
  queryOptions({
    queryKey: healthKeys.status(),
    queryFn: fetchHealth,
    refetchInterval: 30_000,
    retry: false,
  });
