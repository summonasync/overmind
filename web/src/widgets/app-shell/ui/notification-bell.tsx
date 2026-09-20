"use client";

import { useState } from "react";
import { Bell } from "lucide-react";
import { useNotifications } from "@/features/notifications";
import { Badge } from "@/shared/ui/badge";
import { Modal } from "@/shared/ui/modal";
import { NotificationCenter } from "@/widgets/app-shell/ui/notification-center";

const DISPLAY_CAP = 50;

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const { unreadCount } = useNotifications();

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={`Notifications (${unreadCount} unread)`}
        aria-haspopup="dialog"
        className="relative flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-border bg-surface text-muted transition-colors hover:border-border-strong hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
      >
        <Bell size={16} aria-hidden />
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
