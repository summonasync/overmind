import { apiDelete, apiGet, apiPost } from "@/shared/api/client";
import type { BulkResult, NotificationItem, NotificationsResponse } from "@/features/notifications/types";

export interface ListNotificationsParams {
  includeHidden?: boolean;
  awaiting?: boolean;
  take?: number;
}

export function listNotifications(params: ListNotificationsParams = {}): Promise<NotificationsResponse> {
  const search = new URLSearchParams();
  if (params.includeHidden) {
    search.set("includeHidden", "true");
  }
  if (params.awaiting) {
    search.set("awaiting", "true");
  }
  if (params.take !== undefined) {
    search.set("take", String(params.take));
  }
  const query = search.toString();
  return apiGet<NotificationsResponse>(`/notifications${query ? `?${query}` : ""}`);
}

export function markNotificationRead(id: string): Promise<NotificationItem> {
  return apiPost<NotificationItem>(`/notifications/${id}/read`);
}

export function replyToNotification(id: string, text: string): Promise<NotificationItem> {
  return apiPost<NotificationItem>(`/notifications/${id}/reply`, { text });
}

export function deleteNotification(id: string): Promise<void> {
  return apiDelete(`/notifications/${id}`);
}

export function readAllNotifications(): Promise<BulkResult> {
  return apiPost<BulkResult>("/notifications/read-all");
}

export function clearReadNotifications(): Promise<BulkResult> {
  return apiPost<BulkResult>("/notifications/clear-read");
}

export function hideConversations(): Promise<BulkResult> {
  return apiPost<BulkResult>("/notifications/clear-conversations");
}
