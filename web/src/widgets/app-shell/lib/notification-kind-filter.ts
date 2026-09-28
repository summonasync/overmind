export const NOTIFICATION_KIND_FILTERS = ["all", "approval", "question", "info"] as const;

export type NotificationKindFilter = (typeof NOTIFICATION_KIND_FILTERS)[number];
