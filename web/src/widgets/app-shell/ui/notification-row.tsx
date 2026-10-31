"use client";

import { X } from "lucide-react";
import {
  FeatureSlugLabel,
  isHiddenNotification,
  NOTIFICATION_KIND_META,
  notificationHref,
  notificationProjectId,
  NotificationActions,
  useNotificationMutations,
} from "@/features/notifications";
import type { NotificationItem } from "@/features/notifications";
import { FeatureNumberBadge, useFeatureTitle } from "@/features/planning";
import { ProjectNameBadge, useProjectNumbering } from "@/features/projects";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { cn } from "@/shared/lib/cn";
import { formatNotificationTime } from "@/shared/lib/relative-time";

interface NotificationRowProps {
  item: NotificationItem;
  highlighted?: boolean;
}

export function NotificationRow({ item, highlighted }: NotificationRowProps) {
  const projectId = notificationProjectId(item);
  const { markRead, reply, remove } = useNotificationMutations();
  const numbering = useProjectNumbering(projectId);
  const title = useFeatureTitle(projectId, item.featureSlug ?? "");

  const meta = NOTIFICATION_KIND_META[item.kind];
  const KindIcon = meta.icon;
  const archived = isHiddenNotification(item);
  const unread = item.readAt === null || item.readAt === undefined;
  const markingThis = markRead.isPending && markRead.variables === item.id;
  const replyingThis = reply.isPending && reply.variables?.id === item.id;
  const removingThis = remove.isPending && remove.variables === item.id;

  return (
    <li
      data-notification-id={item.id}
      className={cn(
        "group rounded-lg border bg-surface p-3 transition-shadow",
        archived
          ? "border-border/40 opacity-40"
          : unread
            ? "border-primary/30"
            : "border-border opacity-60",
        highlighted && "ring-2 ring-primary/50",
      )}
    >
      <div className="flex items-center gap-2">
        {archived ? <span className="sr-only">Archived</span> : null}
        {item.featureSlug ? (
          <a
            href={notificationHref(item)}
            className="group min-w-0 rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            <span className="flex min-w-0 items-center gap-1.5">
              <FeatureNumberBadge slug={item.featureSlug} numbering={numbering} />
              <span className="min-w-0 truncate text-sm font-semibold text-foreground group-hover:text-primary">
                {title ?? item.featureSlug}
              </span>
            </span>
          </a>
        ) : (
          <ProjectNameBadge projectId={projectId} className="text-sm!" />
        )}
        <Badge tone={meta.tone}>
          <KindIcon size={12} aria-hidden />
          {item.kind}
        </Badge>
        <span className="ml-auto shrink-0 text-xs text-muted">
          {formatNotificationTime(item.createdAt)}
        </span>
        {replyingThis ? null : (
          <Button
            variant="ghost"
            size="sm"
            className="shrink-0 opacity-0 focus-visible:opacity-100 group-hover:opacity-100"
            aria-label="Delete notification"
            disabled={removingThis}
            onClick={() => remove.mutate(item.id)}
          >
            <X size={12} aria-hidden />
          </Button>
        )}
      </div>
      <p className="mt-2 text-sm text-foreground">{item.text}</p>
      <div className="mt-2 flex items-center gap-2">
        <span className="text-xs text-muted">{item.agent}</span>
        {item.featureSlug ? (
          <a
            href={notificationHref(item)}
            onClick={() => {
              if (unread) {
                markRead.mutate(item.id);
              }
            }}
            className="text-xs font-medium text-primary hover:underline"
          >
            Open <FeatureSlugLabel slug={item.featureSlug} numbering={numbering} />
            {item.tab ? ` · ${item.tab}` : ""} →
          </a>
        ) : null}
        {unread ? (
          <Button
            variant="ghost"
            size="sm"
            className="ml-auto shrink-0"
            disabled={markingThis}
            onClick={() => markRead.mutate(item.id)}
          >
            Mark read
          </Button>
        ) : null}
        <ErrorText error={remove.error} />
      </div>
      <NotificationActions item={item} />
    </li>
  );
}
