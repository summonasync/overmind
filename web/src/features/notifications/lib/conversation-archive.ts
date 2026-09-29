import type { NotificationItem } from "../types";

export function isHiddenNotification(item: NotificationItem): boolean {
  return item.hiddenAt != null;
}

export function repliedConversationsCount(items: NotificationItem[]): number {
  return items.filter((item) => item.replyText != null).length;
}
