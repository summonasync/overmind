"use client";

import { useQuery } from "@tanstack/react-query";
import { isAwaitingNotification } from "@/features/notifications/lib/awaiting";
import { awaitingNotificationsQueryOptions } from "@/features/notifications/queries";

export function useAwaitingNotifications() {
  const query = useQuery(awaitingNotificationsQueryOptions());
  return {
    items: (query.data?.items ?? []).filter(isAwaitingNotification),
    isPending: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}
