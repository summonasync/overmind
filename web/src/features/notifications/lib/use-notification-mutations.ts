"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { markNotificationRead, replyToNotification } from "@/features/notifications/api";
import { notificationKeys } from "@/features/notifications/queries";

export function useNotificationMutations() {
  const queryClient = useQueryClient();

  const markRead = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.list() }),
  });

  const reply = useMutation({
    mutationFn: (input: { id: string; text: string }) => replyToNotification(input.id, input.text),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.list() }),
  });

  return { markRead, reply };
}
