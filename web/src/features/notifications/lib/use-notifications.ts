"use client";

import { useQuery } from "@tanstack/react-query";
import { notificationsQueryOptions } from "@/features/notifications/queries";

export function useNotifications() {
  const query = useQuery(notificationsQueryOptions());
  return {
    items: query.data?.items ?? [],
    unreadCount: query.data?.unreadCount ?? 0,
    isPending: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}
