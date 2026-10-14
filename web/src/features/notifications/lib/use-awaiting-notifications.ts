"use client";

import { useQuery } from "@tanstack/react-query";
import { isAwaitingNotification } from "@/features/notifications/lib/awaiting";
import { awaitingNotificationsQueryOptions } from "@/features/notifications/queries";

export function useAwaitingNotifications(enabled = true) {
  const query = useQuery({ ...awaitingNotificationsQueryOptions(), enabled });
  return {
    items: (query.data?.items ?? []).filter(isAwaitingNotification),
    isPending: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}
