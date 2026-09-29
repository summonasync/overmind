"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  clearReadNotifications,
  deleteNotification,
  hideConversations as hideConversationsRequest,
  markNotificationRead,
  readAllNotifications,
  replyToNotification,
} from "@/features/notifications/api";
import { notificationKeys } from "@/features/notifications/queries";
import type { NotificationsResponse } from "@/features/notifications/types";

type ListPatch = (current: NotificationsResponse) => NotificationsResponse;

interface ListContext {
  snapshot: NotificationsResponse | undefined;
}

export function useNotificationMutations() {
  const queryClient = useQueryClient();
  const listKey = notificationKeys.list();

  const patchList = async (patch: ListPatch): Promise<ListContext> => {
    await queryClient.cancelQueries({ queryKey: listKey });
    const snapshot = queryClient.getQueryData<NotificationsResponse>(listKey);
    queryClient.setQueryData<NotificationsResponse>(listKey, (current) =>
      current === undefined ? current : patch(current),
    );
    return { snapshot };
  };

  const restoreList = (context: ListContext | undefined) => {
    if (context?.snapshot !== undefined) {
      queryClient.setQueryData(listKey, context.snapshot);
    }
  };

  const settleList = () => {
    queryClient.invalidateQueries({ queryKey: notificationKeys.all });
  };

  const markRead = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: settleList,
  });

  const reply = useMutation({
    mutationFn: (input: { id: string; text: string }) => replyToNotification(input.id, input.text),
    onMutate: async ({ id, text }) =>
      patchList((current) => {
        const now = new Date().toISOString();
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
      }),
    onError: (_error, _variables, context) => restoreList(context),
    onSettled: settleList,
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteNotification(id),
    onMutate: async (id) =>
      patchList((current) => {
        const target = current.items.find((item) => item.id === id);
        const wasUnread = target != null && target.readAt == null;
        return {
          ...current,
          items: current.items.filter((item) => item.id !== id),
          unreadCount: wasUnread ? Math.max(0, current.unreadCount - 1) : current.unreadCount,
        };
      }),
    onError: (_error, _variables, context) => restoreList(context),
    onSettled: settleList,
  });

  const markAllRead = useMutation({
    mutationFn: readAllNotifications,
    onMutate: async () => {
      const now = new Date().toISOString();
      return patchList((current) => ({
        ...current,
        items: current.items.map((item) => ({ ...item, readAt: item.readAt ?? now })),
        unreadCount: 0,
      }));
    },
    onError: (_error, _variables, context) => restoreList(context),
    onSettled: settleList,
  });

  const clearRead = useMutation({
    mutationFn: clearReadNotifications,
    onMutate: async () =>
      patchList((current) => ({
        ...current,
        items: current.items.filter((item) => item.readAt == null || item.replyText != null),
      })),
    onError: (_error, _variables, context) => restoreList(context),
    onSettled: settleList,
  });

  const hideConversations = useMutation({
    mutationFn: hideConversationsRequest,
    onSuccess: settleList,
  });

  return { markRead, reply, remove, markAllRead, clearRead, hideConversations };
}
