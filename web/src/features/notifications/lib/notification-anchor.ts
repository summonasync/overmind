export const NOTIFICATION_PARAM = "notification";

export const NOTIFICATION_HIGHLIGHT_MS = 2000;

export const ANCHOR_POLL_INTERVAL_MS = 150;

export const ANCHOR_POLL_TIMEOUT_MS = 4000;

export function attentionHref(notificationId: string): string {
  return `/attention?${NOTIFICATION_PARAM}=${encodeURIComponent(notificationId)}`;
}

export function notificationAnchorSelector(notificationId: string): string {
  const escaped = notificationId.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
  return `[data-notification-id="${escaped}"]`;
}
