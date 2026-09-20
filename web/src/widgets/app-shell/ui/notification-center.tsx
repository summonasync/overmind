"use client";

import { useNotifications } from "@/features/notifications";
import { ErrorText } from "@/shared/ui/error-text";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { NotificationRow } from "@/widgets/app-shell/ui/notification-row";

export function NotificationCenter() {
  const { items, isPending, error } = useNotifications();

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

  return (
    <ul className="flex flex-col gap-2">
      {items.map((item) => (
        <NotificationRow key={item.id} item={item} />
      ))}
    </ul>
  );
}
