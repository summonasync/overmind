"use client";

import { useState } from "react";
import { CircleHelp, Info, Send, TriangleAlert } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { featureNumber } from "@/features/planning";
import { useNotificationMutations } from "@/features/notifications";
import type { NotificationItem, NotificationKind } from "@/features/notifications";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { cn } from "@/shared/lib/cn";

const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

const kindMeta: Record<NotificationKind, { tone: BadgeTone; icon: LucideIcon }> = {
  approval: { tone: "warning", icon: TriangleAlert },
  question: { tone: "info", icon: CircleHelp },
  info: { tone: "neutral", icon: Info },
};

function formatNotificationTime(iso: string): string {
  const timestamp = Date.parse(iso);
  if (Number.isNaN(timestamp)) {
    return "";
  }
  const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
  if (seconds < MINUTE) {
    return "just now";
  }
  if (seconds < HOUR) {
    return `${Math.floor(seconds / MINUTE)}m ago`;
  }
  if (seconds < DAY) {
    return `${Math.floor(seconds / HOUR)}h ago`;
  }
  return `${Math.floor(seconds / DAY)}d ago`;
}

function notificationHref(item: NotificationItem): string {
  const params = new URLSearchParams();
  if (item.projectId) {
    params.set("project", item.projectId);
  }
  params.set("feature", item.featureSlug ?? "");
  if (item.tab) {
    params.set("tab", item.tab);
  }
  return `/dashboard?${params.toString()}`;
}

function featureSlugLabel(slug: string) {
  const number = featureNumber(slug);
  if (number === null) {
    return slug;
  }
  return (
    <>
      <span className="font-mono font-semibold">{number}</span>
      {slug.slice(number.length)}
    </>
  );
}

interface NotificationRowProps {
  item: NotificationItem;
}

export function NotificationRow({ item }: NotificationRowProps) {
  const [draft, setDraft] = useState("");
  const { markRead, reply } = useNotificationMutations();

  const meta = kindMeta[item.kind];
  const KindIcon = meta.icon;
  const existingReply = typeof item.replyText === "string" ? item.replyText : null;
  const replied = existingReply !== null;
  const unread = item.readAt === null || item.readAt === undefined;
  const markingThis = markRead.isPending && markRead.variables === item.id;
  const replyingThis = reply.isPending && reply.variables?.id === item.id;

  const sendReply = () => {
    const text = draft.trim();
    if (!text) {
      return;
    }
    reply.mutate({ id: item.id, text }, { onSuccess: () => setDraft("") });
  };

  return (
    <li
      className={cn(
        "rounded-lg border bg-surface p-3",
        unread ? "border-primary/30" : "border-border opacity-60",
      )}
    >
      <div className="flex items-center gap-2">
        <Badge tone={meta.tone}>
          <KindIcon size={12} aria-hidden />
          {item.kind}
        </Badge>
        <span className="text-xs font-medium text-foreground">{item.agent}</span>
        <span className="ml-auto shrink-0 text-xs text-muted">
          {formatNotificationTime(item.createdAt)}
        </span>
      </div>
      <p className="mt-2 text-sm text-foreground">{item.text}</p>
      <div className="mt-2 flex items-center gap-2">
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
            Open {featureSlugLabel(item.featureSlug)}
            {item.tab ? ` · ${item.tab}` : ""} →
          </a>
        ) : null}
        {unread ? (
          <Button
            variant="ghost"
            className="ml-auto shrink-0 px-2 py-1 text-xs"
            disabled={markingThis}
            onClick={() => markRead.mutate(item.id)}
          >
            Mark read
          </Button>
        ) : null}
      </div>
      {item.kind !== "info" ? (
        <form
          className="mt-2 flex items-center gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            sendReply();
          }}
        >
          <Input
            value={existingReply ?? draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={replied ? undefined : "Reply to the agent…"}
            disabled={replied || replyingThis}
            aria-label={`Reply to ${item.agent}`}
            className="text-xs"
          />
          <Button
            type="submit"
            variant="accent"
            className="shrink-0 px-2 py-1 text-xs"
            disabled={replied || replyingThis || draft.trim().length === 0}
          >
            <Send size={12} aria-hidden />
            Send
          </Button>
          <ErrorText error={reply.error} />
        </form>
      ) : null}
    </li>
  );
}
