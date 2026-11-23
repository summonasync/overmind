"use client";

import { X } from "lucide-react";
import {
  NotificationActions,
  NOTIFICATION_KIND_META,
  attentionHref,
} from "@/features/notifications";
import type { NotificationItem } from "@/features/notifications/types";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";

interface AttentionToastProps {
  item: NotificationItem;
  onDismiss: (id: string) => void;
}

// The visible-tab counterpart of the native toast: the OS card cannot carry a link or an
// answer field, so while the app is on screen this card does both — the header jumps to the
// anchored row on /attention, and approval/question items answer inline through the same
// reply endpoint the attention page uses.
export function AttentionToast({ item, onDismiss }: AttentionToastProps) {
  const meta = NOTIFICATION_KIND_META[item.kind];
  const KindIcon = meta.icon;

  return (
    <section
      aria-label={`${item.kind} from ${item.agent}`}
      className="pointer-events-auto w-80 overflow-hidden rounded-lg border border-warning/40 bg-surface-raised shadow-xl"
    >
      <header className="flex items-center gap-1.5 border-b border-border/60 px-3 py-2">
        <KindIcon size={14} className={meta.iconClass} aria-hidden />
        <a
          href={attentionHref(item.id)}
          className="text-sm font-semibold text-foreground hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          Attention
        </a>
        <span className="text-[10px] uppercase tracking-wide text-muted">click</span>
        <span className="ml-auto flex items-center gap-1">
          <Badge tone={meta.tone} size="xs">
            {item.kind}
          </Badge>
          <Button variant="ghost" size="xs" onClick={() => onDismiss(item.id)} aria-label="Dismiss">
            <X size={12} aria-hidden />
          </Button>
        </span>
      </header>
      <p className="max-h-32 overflow-y-auto px-3 py-2 text-xs leading-5 whitespace-pre-line text-foreground">
        {item.text}
      </p>
      <div className="px-3 pb-2">
        <NotificationActions item={item} />
      </div>
    </section>
  );
}
