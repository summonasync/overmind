"use client";

import { useState } from "react";
import { Bell } from "lucide-react";
import {
  isAwaitingApproval,
  NOTIFICATION_KIND_META,
  notificationHref,
  unreadNotifications,
  useNotifications,
} from "@/features/notifications";
import type { NotificationItem } from "@/features/notifications";
import { Badge } from "@/shared/ui/badge";
import { EmptyState } from "@/shared/ui/empty-state";
import { Modal } from "@/shared/ui/modal";
import { cn } from "@/shared/lib/cn";
import { formatNotificationTime } from "@/shared/lib/relative-time";

const DISPLAY_CAP = 50;

function BellPreviewRow({ item }: { item: NotificationItem }) {
  const meta = NOTIFICATION_KIND_META[item.kind];
  const KindIcon = meta.icon;
  const href = item.featureSlug ? notificationHref(item) : null;

  const content = (
    <>
      <KindIcon size={14} aria-hidden className={cn("shrink-0", meta.iconClass)} />
      <span className="min-w-0 flex-1 truncate text-sm text-foreground">{item.text}</span>
      <span className="shrink-0 text-xs text-muted">{formatNotificationTime(item.createdAt)}</span>
    </>
  );

  return (
    <li>
      {href ? (
        <a
          href={href}
          className="flex items-center gap-2 rounded-md px-2 py-2 transition-colors hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          {content}
        </a>
      ) : (
        <div className="flex items-center gap-2 px-2 py-2">{content}</div>
      )}
    </li>
  );
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const { items, unreadCount } = useNotifications();
  const awaitingApproval = items.some(isAwaitingApproval);
  const unread = unreadNotifications(items);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={`Notifications (${unreadCount} unread)${awaitingApproval ? ", approvals awaiting reply" : ""}`}
        aria-haspopup="dialog"
        className={cn(
          "relative flex h-9 w-9 shrink-0 items-center justify-center rounded-full border bg-surface transition-colors hover:border-border-strong hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
          awaitingApproval ? "border-warning/60 text-warning" : "border-border text-muted",
        )}
      >
        <Bell size={16} aria-hidden />
        {awaitingApproval ? (
          <span
            className="absolute -left-1.5 -top-1.5 h-2.5 w-2.5 animate-pulse rounded-full bg-warning ring-2 ring-background"
            aria-hidden
          />
        ) : null}
        {unreadCount > 0 ? (
          <Badge
            tone="danger"
            className="absolute -right-1.5 -top-1.5 min-w-4 justify-center px-1 text-[10px] leading-4"
          >
            {unreadCount > DISPLAY_CAP ? `${DISPLAY_CAP}+` : unreadCount}
          </Badge>
        ) : null}
      </button>
      <Modal title="Notifications" open={open} onClose={() => setOpen(false)}>
        <div className="flex flex-col gap-3">
          {unread.length === 0 ? (
            <EmptyState
              title="No unread notifications"
              hint="Everything is read — the Attention page holds the awaiting queue and the full history."
            />
          ) : (
            <ul className="flex flex-col">
              {unread.map((item) => (
                <BellPreviewRow key={item.id} item={item} />
              ))}
            </ul>
          )}
          <a
            href="/attention"
            onClick={() => setOpen(false)}
            className="inline-flex items-center justify-center gap-1.5 rounded-md border border-primary/40 bg-transparent px-3 py-1.5 text-sm font-medium text-primary transition-colors hover:border-primary/60 hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
          >
            <Bell size={14} aria-hidden />
            Open Attention
          </a>
        </div>
      </Modal>
    </>
  );
}
