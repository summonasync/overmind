import type { NotificationItem } from "../types";

export function notificationProjectId(item: NotificationItem): string {
  return item.projectId ?? "";
}
