import { queryOptions } from "@tanstack/react-query";
import { listNotifications } from "@/features/notifications/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const notificationKeys = {
  all: ["notifications"] as const,
  list: () => [...notificationKeys.all, "list"] as const,
};

export const notificationsQueryOptions = () =>
  queryOptions({
    queryKey: notificationKeys.list(),
    queryFn: listNotifications,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });
