import { apiGet, apiPost } from "@/shared/api/client";
import type { NotificationItem, NotificationsResponse } from "@/features/notifications/types";

export function listNotifications(): Promise<NotificationsResponse> {
  return apiGet<NotificationsResponse>("/notifications");
}

export function markNotificationRead(id: string): Promise<NotificationItem> {
  return apiPost<NotificationItem>(`/notifications/${id}/read`);
}

export function replyToNotification(id: string, text: string): Promise<NotificationItem> {
  return apiPost<NotificationItem>(`/notifications/${id}/reply`, { text });
}
