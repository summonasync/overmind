import { keepPreviousData, queryOptions } from "@tanstack/react-query";
import { listNotifications } from "@/features/notifications/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const AWAITING_TAKE = 200;

export const notificationKeys = {
  all: ["notifications"] as const,
  list: (includeHidden = false) => [...notificationKeys.all, "list", { includeHidden }] as const,
  awaiting: () => [...notificationKeys.all, "awaiting"] as const,
};

export const notificationsQueryOptions = (includeHidden = false) =>
  queryOptions({
    queryKey: notificationKeys.list(includeHidden),
    queryFn: () => listNotifications({ includeHidden }),
    placeholderData: keepPreviousData,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const awaitingNotificationsQueryOptions = () =>
  queryOptions({
    queryKey: notificationKeys.awaiting(),
    queryFn: () => listNotifications({ awaiting: true, take: AWAITING_TAKE }),
    placeholderData: keepPreviousData,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
