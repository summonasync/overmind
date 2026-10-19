import { queryOptions } from "@tanstack/react-query";
import { getGrimoireStatus } from "@/features/grimoire/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const grimoireKeys = {
  all: ["grimoire"] as const,
  status: () => [...grimoireKeys.all, "status"] as const,
};

export const grimoireStatusQueryOptions = () =>
  queryOptions({
    queryKey: grimoireKeys.status(),
    queryFn: getGrimoireStatus,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
