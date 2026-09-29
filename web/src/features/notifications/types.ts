export type NotificationKind = "approval" | "question" | "info";

export type NotificationTab = "spec" | "plan" | "tasks" | "result";

export interface NotificationItem {
  id: string;
  projectId?: string | null;
  kind: NotificationKind;
  featureSlug?: string | null;
  tab?: NotificationTab | null;
  text: string;
  agent: string;
  createdAt: string;
  readAt?: string | null;
  replyText?: string | null;
  repliedAt?: string | null;
  hiddenAt?: string | null;
}

export interface NotificationsResponse {
  items: NotificationItem[];
  unreadCount: number;
}

export type BulkResult = { marked: number } | { deleted: number } | { hidden: number };
