import type { NotificationItem } from "../types";

export function unreadNotifications(items: NotificationItem[]): NotificationItem[] {
  return items.filter((item) => item.readAt == null);
}
