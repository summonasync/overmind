"use client";

import {
  FeatureSlugLabel,
  NOTIFICATION_KIND_META,
  notificationHref,
  notificationProjectId,
  NotificationActions,
} from "@/features/notifications";
import type { NotificationItem } from "@/features/notifications";
import { ProjectNameBadge, useProjectNumbering } from "@/features/projects";
import { cn } from "@/shared/lib/cn";
import { formatNotificationTime } from "@/shared/lib/relative-time";

interface AwaitingRowProps {
  item: NotificationItem;
}

export function AwaitingRow({ item }: AwaitingRowProps) {
  const meta = NOTIFICATION_KIND_META[item.kind];
  const KindIcon = meta.icon;
  const numbering = useProjectNumbering(notificationProjectId(item));

  return (
    <li
      className={cn(
        "rounded-lg border bg-surface p-3",
        item.kind === "approval" ? "border-warning/30" : "border-primary/30",
      )}
    >
      <div className="flex items-center gap-2">
        <ProjectNameBadge projectId={notificationProjectId(item)} className="text-sm!" />
        <KindIcon size={14} aria-hidden className={cn("shrink-0", meta.iconClass)} />
        <span className="text-xs text-muted">{item.agent}</span>
        {item.featureSlug ? (
          <span className="text-xs text-muted">
            <FeatureSlugLabel slug={item.featureSlug} numbering={numbering} />
          </span>
        ) : null}
        <span className="ml-auto shrink-0 text-xs text-muted">
          {formatNotificationTime(item.createdAt)}
        </span>
      </div>
      <p className="mt-2 text-sm text-foreground">{item.text}</p>
      {item.featureSlug ? (
        <a
          href={notificationHref(item)}
          className="mt-2 inline-block text-xs font-medium text-primary hover:underline"
        >
          Open <FeatureSlugLabel slug={item.featureSlug} numbering={numbering} />
          {item.tab ? ` · ${item.tab}` : ""} →
        </a>
      ) : null}
      <NotificationActions item={item} />
    </li>
  );
}
