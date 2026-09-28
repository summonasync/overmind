"use client";

import { useState } from "react";
import { useNotificationMutations, useNotifications } from "@/features/notifications";
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
  const { items, unreadCount, isPending, error } = useNotifications();
  const { markAllRead, clearRead } = useNotificationMutations();
  const [kind, setKind] = useState<NotificationKindFilter>(initialKind);
  const readCount = items.filter((item) => item.readAt != null).length;

  if (isPending) {
    return <LoadingState label="Loading notifications…" />;
  }

  if (error) {
    return <ErrorText error={error} />;
  }

  if (items.length === 0) {
    return (
      <EmptyState
        title="No notifications"
        hint="Agents can reach you here via overmind_notify — quiet for now."
      />
    );
  }

  const visible = kind === "all" ? items : items.filter((item) => item.kind === kind);
  const bulkError = markAllRead.error ?? clearRead.error;

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
        </div>
      </div>
      {bulkError ? <ErrorText error={bulkError} /> : null}
      {visible.length === 0 ? (
        <EmptyState
          title="Nothing under this filter"
          hint="No notifications of this kind — switch the filter to see the rest."
        />
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
