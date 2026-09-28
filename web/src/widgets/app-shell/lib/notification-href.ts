import type { NotificationItem } from "@/features/notifications";

export function notificationHref(item: NotificationItem): string {
  const params = new URLSearchParams();
  if (item.projectId) {
    params.set("project", item.projectId);
  }
  params.set("feature", item.featureSlug ?? "");
  if (item.tab) {
    params.set("tab", item.tab);
  }
  return `/dashboard?${params.toString()}`;
}
