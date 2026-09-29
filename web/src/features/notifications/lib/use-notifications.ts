"use client";

import { useQuery } from "@tanstack/react-query";
import { notificationsQueryOptions } from "@/features/notifications/queries";

export function useNotifications(includeHidden = false) {
  const query = useQuery(notificationsQueryOptions(includeHidden));
  return {
    items: query.data?.items ?? [],
    unreadCount: query.data?.unreadCount ?? 0,
    isPending: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}
