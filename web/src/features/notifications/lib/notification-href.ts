import type { NotificationItem } from "../types";

export function notificationHref(item: NotificationItem): string {
  if (item.tab === "issues") {
    const params = new URLSearchParams();
    if (item.projectId) {
      params.set("project", item.projectId);
    }
    if (item.featureSlug) {
      params.set("issue", item.featureSlug);
    }
    return `/issues?${params.toString()}`;
  }

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
