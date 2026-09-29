import { keepPreviousData, queryOptions } from "@tanstack/react-query";
import { listNotifications } from "@/features/notifications/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const notificationKeys = {
  all: ["notifications"] as const,
  list: (includeHidden = false) => [...notificationKeys.all, "list", { includeHidden }] as const,
};

export const notificationsQueryOptions = (includeHidden = false) =>
  queryOptions({
    queryKey: notificationKeys.list(includeHidden),
    queryFn: () => listNotifications(includeHidden),
    placeholderData: keepPreviousData,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
