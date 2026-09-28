"use client";

import { useState } from "react";
import { Check, CircleHelp, Info, Send, TriangleAlert, X } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { featureNumber } from "@/features/planning";
import { APPROVAL_REPLY, approvalChoice, useNotificationMutations } from "@/features/notifications";
import type { NotificationItem, NotificationKind } from "@/features/notifications";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { cn } from "@/shared/lib/cn";
import { formatNotificationTime } from "@/shared/lib/relative-time";
import { notificationHref } from "@/widgets/app-shell/lib/notification-href";

const kindMeta: Record<NotificationKind, { tone: BadgeTone; icon: LucideIcon }> = {
  approval: { tone: "warning", icon: TriangleAlert },
  question: { tone: "info", icon: CircleHelp },
  info: { tone: "neutral", icon: Info },
};

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
  const choice = approvalChoice(existingReply);
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
      {/* No server denial semantics: reject posts a regular reply, 077 proof search reads any reply — backend left unchanged by user decision. */}
      {item.kind === "approval" ? (
        <div className="mt-2 flex items-center gap-2">
          <Button
            variant="secondary"
            className="shrink-0 px-2 py-1 text-xs"
            disabled={replied || replyingThis}
            onClick={() => reply.mutate({ id: item.id, text: APPROVAL_REPLY.approve })}
            aria-label={`Approve request from ${item.agent}`}
          >
            <Check size={12} className="text-success" aria-hidden />
            Approve
          </Button>
          <Button
            variant="danger"
            className="shrink-0 px-2 py-1 text-xs"
            disabled={replied || replyingThis}
            onClick={() => reply.mutate({ id: item.id, text: APPROVAL_REPLY.reject })}
            aria-label={`Reject request from ${item.agent}`}
            title={`Sends reply "rejected"; it does not block the agent's validation by itself`}
          >
            <X size={12} aria-hidden />
            Reject
          </Button>
          {replied ? (
            <span
              className={cn(
                "text-xs font-medium",
                choice === "approve" && "text-success",
                choice === "reject" && "text-danger",
                choice === null && "text-muted",
              )}
            >
              Reply: {existingReply}
            </span>
          ) : null}
          <ErrorText error={reply.error} />
        </div>
      ) : item.kind === "question" ? (
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
