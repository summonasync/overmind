import { apiDelete, apiGet, apiPost } from "@/shared/api/client";
import type { BulkResult, NotificationItem, NotificationsResponse } from "@/features/notifications/types";

export function listNotifications(includeHidden = false): Promise<NotificationsResponse> {
  return apiGet<NotificationsResponse>(`/notifications${includeHidden ? "?includeHidden=true" : ""}`);
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
