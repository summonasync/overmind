import type { NotificationItem } from "../types";

export function isAwaitingNotification(item: NotificationItem): boolean {
  if (item.hiddenAt != null || typeof item.replyText === "string") {
    return false;
  }
  return item.kind === "approval" || item.kind === "question";
}
