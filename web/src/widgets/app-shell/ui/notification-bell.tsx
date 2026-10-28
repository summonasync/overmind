"use client";

import { useMemo, useState } from "react";
import { Bell } from "lucide-react";
import {
  isAwaitingApproval,
  unreadNotifications,
  useBellAutoRead,
  useNotificationMutations,
  useNotifications,
} from "@/features/notifications";
import { Badge } from "@/shared/ui/badge";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { cn } from "@/shared/lib/cn";
import { BellNotificationCard } from "@/widgets/app-shell/ui/bell-notification-card";

const DISPLAY_CAP = 50;

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const { items, unreadCount } = useNotifications();
  const { markRead } = useNotificationMutations();
  const awaitingApproval = items.some(isAwaitingApproval);
  const unread = unreadNotifications(items);
  const unreadIds = useMemo(() => unread.map((item) => item.id), [unread]);

  useBellAutoRead(open, unreadIds, markRead.mutate);

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
            size="xs"
            className="absolute -right-1.5 -top-1.5 min-w-4 justify-center"
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
            <ul className="flex flex-col gap-2">
              {unread.map((item) => (
                <BellNotificationCard
                  key={item.id}
                  item={item}
                  marking={markRead.isPending && markRead.variables === item.id}
                  onMarkRead={markRead.mutate}
                />
              ))}
            </ul>
          )}
          <ErrorText error={markRead.error} />
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
