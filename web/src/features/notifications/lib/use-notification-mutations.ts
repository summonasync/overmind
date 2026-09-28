"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { markNotificationRead, replyToNotification } from "@/features/notifications/api";
import { notificationKeys } from "@/features/notifications/queries";
import type { NotificationsResponse } from "@/features/notifications/types";

export function useNotificationMutations() {
  const queryClient = useQueryClient();
  const listKey = notificationKeys.list();

  const markRead = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: listKey }),
  });

  const reply = useMutation({
    mutationFn: (input: { id: string; text: string }) => replyToNotification(input.id, input.text),
    onMutate: async ({ id, text }) => {
      await queryClient.cancelQueries({ queryKey: listKey });
      const snapshot = queryClient.getQueryData<NotificationsResponse>(listKey);
      const now = new Date().toISOString();
      queryClient.setQueryData<NotificationsResponse>(listKey, (current) => {
        if (current === undefined) {
          return current;
        }
        let becameRead = false;
        const items = current.items.map((item) => {
          if (item.id !== id) {
            return item;
          }
          if (item.readAt == null) {
            becameRead = true;
          }
          return { ...item, replyText: text, repliedAt: now, readAt: item.readAt ?? now };
        });
        return {
          ...current,
          items,
          unreadCount: Math.max(0, current.unreadCount - (becameRead ? 1 : 0)),
        };
      });
      return { snapshot };
    },
    onError: (_error, _variables, context) => {
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(listKey, context.snapshot);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: listKey });
    },
  });

  return { markRead, reply };
}
