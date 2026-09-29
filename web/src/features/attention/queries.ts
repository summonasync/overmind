import { queryOptions } from "@tanstack/react-query";
import { listCeremonies } from "@/features/attention/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const attentionKeys = {
  all: ["attention"] as const,
  ceremonies: () => [...attentionKeys.all, "ceremonies"] as const,
};

export const ceremoniesQueryOptions = () =>
  queryOptions({
    queryKey: attentionKeys.ceremonies(),
    queryFn: listCeremonies,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
