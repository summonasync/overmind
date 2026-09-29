"use client";

import { useState } from "react";
import {
  repliedConversationsCount,
  useNotificationMutations,
  useNotifications,
} from "@/features/notifications";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { cn } from "@/shared/lib/cn";
import { NOTIFICATION_KIND_FILTERS } from "@/widgets/app-shell/lib/notification-kind-filter";
import type { NotificationKindFilter } from "@/widgets/app-shell/lib/notification-kind-filter";
import { NotificationRow } from "@/widgets/app-shell/ui/notification-row";

const chipBase =
  "inline-flex items-center rounded-md border px-2 py-1 text-xs font-medium capitalize transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40";
const chipInactive = "border-border text-muted hover:border-border-strong hover:text-foreground";
const chipActive = "border-primary bg-primary/10 text-primary";

interface NotificationCenterProps {
  initialKind?: NotificationKindFilter;
}

export function NotificationCenter({ initialKind = "all" }: NotificationCenterProps) {
  const [kind, setKind] = useState<NotificationKindFilter>(initialKind);
  const [showHidden, setShowHidden] = useState(false);
  const { items, unreadCount, isPending, error } = useNotifications(showHidden);
  const { markAllRead, clearRead, hideConversations } = useNotificationMutations();
  const readCount = items.filter((item) => item.readAt != null).length;
  const repliedCount = repliedConversationsCount(items);

  if (isPending) {
    return <LoadingState label="Loading notifications…" />;
  }

  if (error) {
    return <ErrorText error={error} />;
  }

  const visible = kind === "all" ? items : items.filter((item) => item.kind === kind);
  const bulkError = markAllRead.error ?? clearRead.error ?? hideConversations.error;

  let emptyTitle = "Nothing under this filter";
  let emptyHint = "No notifications of this kind — switch the filter to see the rest.";
  if (items.length === 0) {
    emptyTitle = showHidden ? "Nothing hidden" : "No notifications";
    emptyHint = showHidden
      ? "Cleared conversations land in the archive — nothing there yet."
      : "Agents can reach you here via overmind_notify — quiet for now.";
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <div role="group" aria-label="Filter by kind" className="flex flex-wrap items-center gap-1.5">
          {NOTIFICATION_KIND_FILTERS.map((filter) => (
            <button
              key={filter}
              type="button"
              aria-pressed={kind === filter}
              onClick={() => setKind(filter)}
              className={cn(chipBase, kind === filter ? chipActive : chipInactive)}
            >
              {filter}
            </button>
          ))}
        </div>
        <div className="ml-auto flex items-center gap-1.5">
          <Button
            variant="ghost"
            className="px-2 py-1 text-xs"
            disabled={unreadCount === 0 || markAllRead.isPending}
            onClick={() => markAllRead.mutate()}
          >
            Mark all read
          </Button>
          <Button
            variant="ghost"
            className="px-2 py-1 text-xs"
            disabled={readCount === 0 || clearRead.isPending}
            onClick={() => clearRead.mutate()}
          >
            Clear read
          </Button>
          <Button
            variant="ghost"
            className="px-2 py-1 text-xs"
            disabled={repliedCount === 0 || hideConversations.isPending}
            onClick={() => hideConversations.mutate()}
          >
            Clear conversations
          </Button>
          <button
            type="button"
            aria-pressed={showHidden}
            onClick={() => setShowHidden((current) => !current)}
            className={cn(chipBase, showHidden ? chipActive : chipInactive)}
          >
            Show hidden
          </button>
        </div>
      </div>
      {bulkError ? <ErrorText error={bulkError} /> : null}
      {visible.length === 0 ? (
        <EmptyState title={emptyTitle} hint={emptyHint} />
      ) : (
        <ul className="flex flex-col gap-2">
          {visible.map((item) => (
            <NotificationRow key={item.id} item={item} />
          ))}
        </ul>
      )}
    </div>
  );
}
