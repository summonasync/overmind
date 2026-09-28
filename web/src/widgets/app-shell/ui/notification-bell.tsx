"use client";

import { useState } from "react";
import { Bell } from "lucide-react";
import { isAwaitingApproval, useNotifications } from "@/features/notifications";
import { Badge } from "@/shared/ui/badge";
import { Modal } from "@/shared/ui/modal";
import { cn } from "@/shared/lib/cn";
import { NotificationCenter } from "@/widgets/app-shell/ui/notification-center";

const DISPLAY_CAP = 50;

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const { items, unreadCount } = useNotifications();
  const awaitingApproval = items.some(isAwaitingApproval);

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
        <NotificationCenter />
      </Modal>
    </>
  );
}
