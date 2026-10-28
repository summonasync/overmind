"use client";

import { useNotifications } from "@/features/notifications";
import { Badge } from "@/shared/ui/badge";

const DISPLAY_CAP = 99;

export function AttentionBadge() {
  const { unreadCount } = useNotifications();
  if (unreadCount === 0) {
    return null;
  }
  return (
    <Badge tone="danger" size="xs" className="ml-auto min-w-5 justify-center">
      {unreadCount > DISPLAY_CAP ? `${DISPLAY_CAP}+` : unreadCount}
    </Badge>
  );
}
