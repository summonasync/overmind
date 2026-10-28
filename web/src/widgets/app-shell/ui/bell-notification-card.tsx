"use client";

import {
  NOTIFICATION_KIND_META,
  attentionHref,
  notificationHref,
  notificationProjectId,
} from "@/features/notifications";
import type { NotificationItem } from "@/features/notifications";
import { FeatureNumberBadge, useFeatureTitle } from "@/features/planning";
import { ProjectNameBadge, useProjectNumbering } from "@/features/projects";
import { Button } from "@/shared/ui/button";
import { cn } from "@/shared/lib/cn";
import { formatNotificationTime } from "@/shared/lib/relative-time";

interface BellNotificationCardProps {
  item: NotificationItem;
  marking: boolean;
  onMarkRead: (id: string) => void;
}

export function BellNotificationCard({ item, marking, onMarkRead }: BellNotificationCardProps) {
  const projectId = notificationProjectId(item);
  const numbering = useProjectNumbering(projectId);
  const title = useFeatureTitle(projectId, item.featureSlug ?? "");

  const meta = NOTIFICATION_KIND_META[item.kind];
  const KindIcon = meta.icon;

  const header = item.featureSlug ? (
    <span className="flex min-w-0 items-center gap-1.5">
      <FeatureNumberBadge slug={item.featureSlug} numbering={numbering} />
      <span className="min-w-0 truncate text-sm font-semibold text-foreground">
        {title ?? item.featureSlug}
      </span>
    </span>
  ) : (
    <ProjectNameBadge projectId={projectId} className="text-sm!" />
  );

  return (
    <li className="rounded-lg border border-primary/30 bg-surface p-3">
      <a
        href={attentionHref(item.id)}
        aria-label="Show in Attention"
        className="-m-1 block rounded-md p-1 transition-colors hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        <div className="flex items-center gap-2">
          {header}
          <span className="ml-auto shrink-0 text-xs text-muted">
            {formatNotificationTime(item.createdAt)}
          </span>
        </div>
        <div className="mt-2 flex items-center gap-2">
          <KindIcon size={14} aria-hidden className={cn("shrink-0", meta.iconClass)} />
          <p className="min-w-0 flex-1 text-sm text-foreground">{item.text}</p>
        </div>
      </a>
      <div className="mt-2 flex items-center gap-2">
        <span className="text-xs text-muted">{item.agent}</span>
        <Button
          variant="ghost"
          size="sm"
          disabled={marking}
          onClick={() => onMarkRead(item.id)}
        >
          Mark read
        </Button>
        {item.featureSlug ? (
          <a
            href={notificationHref(item)}
            className="ml-auto rounded-md text-xs font-medium text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            Open feature →
          </a>
        ) : null}
      </div>
    </li>
  );
}
