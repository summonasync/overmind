"use client";

import { useEffect, useRef, type RefObject } from "react";
import { Button } from "@/shared/ui/button";
import { cn } from "@/shared/lib/cn";
import { EmptyState } from "@/shared/ui/empty-state";
import { formatRelativeTime } from "@/shared/lib/relative-time";
import { authorLabel, roleTextClassOf } from "@/features/wave-chat/lib/role-color";
import { sliceByUnread } from "@/features/wave-chat/lib/history-window";
import type { UnreadWindow } from "@/features/wave-chat/lib/history-window";
import type { WaveMessage } from "@/features/wave-chat/types";

const CONTEXT_TAIL_LIMIT = 5;

interface WaveMessageListProps {
  messages: WaveMessage[];
  window: Pick<UnreadWindow, "from" | "to">;
  ownerAlias: string;
  canPageBack: boolean;
  paging: boolean;
  expanded: boolean;
  onlyUnread: boolean;
  onShowMore: () => Promise<void>;
  onShowAll: () => Promise<void>;
  scrollKey: string;
}

export function WaveMessageList({
  messages,
  window,
  ownerAlias,
  canPageBack,
  paging,
  expanded,
  onlyUnread,
  onShowMore,
  onShowAll,
  scrollKey,
}: WaveMessageListProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const unreadAnchorRef = useRef<HTMLLIElement | null>(null);
  const { read, unread, live } = sliceByUnread(messages, window);
  const hasUnread = unread.length > 0;
  const context = onlyUnread
    ? []
    : hasUnread && !expanded
      ? read.slice(-CONTEXT_TAIL_LIMIT)
      : read;

  useEffect(() => {
    if (hasUnread) {
      unreadAnchorRef.current?.scrollIntoView({ block: "start" });
      return;
    }
    const container = containerRef.current;
    if (container !== null) {
      container.scrollTop = container.scrollHeight;
    }
  }, [scrollKey, hasUnread]);

  if (messages.length === 0) {
    return (
      <EmptyState
        title="No messages yet"
        hint="Post a signal below — lane agents pick it up on their next unread fetch."
      />
    );
  }

  if (onlyUnread && !hasUnread && live.length === 0) {
    return (
      <EmptyState title="No unread messages" hint="Everything in this room has been read." />
    );
  }

  const pageKeepingViewport = (load: () => Promise<void>) => async () => {
    const container = containerRef.current;
    const previousTop = container?.scrollTop ?? 0;
    const previousHeight = container?.scrollHeight ?? 0;
    await load();
    if (container !== null) {
      container.scrollTop = container.scrollHeight - previousHeight + previousTop;
    }
  };

  return (
    <div ref={containerRef} className="max-h-[60vh] overflow-y-auto rounded-lg border border-border bg-surface">
      {canPageBack && !onlyUnread ? (
        <div className="flex items-center justify-center gap-2 border-b border-border px-3 py-2">
          <Button variant="ghost" className="text-xs" disabled={paging} onClick={pageKeepingViewport(onShowMore)}>
            Show more
          </Button>
          <Button variant="ghost" className="text-xs" disabled={paging} onClick={pageKeepingViewport(onShowAll)}>
            Show all
          </Button>
        </div>
      ) : null}
      <ol className="flex flex-col gap-2 p-3">
        {context.map((message) => (
          <MessageRow key={message.id} message={message} ownerAlias={ownerAlias} dimmed />
        ))}
        {unread.map((message, index) => (
          <MessageRow
            key={message.id}
            message={message}
            ownerAlias={ownerAlias}
            unread
            anchorRef={index === 0 ? unreadAnchorRef : undefined}
          />
        ))}
        {hasUnread && live.length > 0 ? (
          <li aria-hidden="true" className="flex items-center gap-2 px-1 text-xs text-muted">
            <span className="h-px flex-1 bg-border" />
            new
            <span className="h-px flex-1 bg-border" />
          </li>
        ) : null}
        {live.map((message) => (
          <MessageRow key={message.id} message={message} ownerAlias={ownerAlias} />
        ))}
      </ol>
    </div>
  );
}

interface MessageRowProps {
  message: WaveMessage;
  ownerAlias: string;
  dimmed?: boolean;
  unread?: boolean;
  anchorRef?: RefObject<HTMLLIElement | null>;
}

function MessageRow({ message, ownerAlias, dimmed = false, unread = false, anchorRef }: MessageRowProps) {
  return (
    <li
      ref={anchorRef}
      aria-current={unread ? "true" : undefined}
      className={cn(
        "rounded-md px-2.5 py-2",
        message.authorAlias === ownerAlias ? "bg-primary/5" : "bg-surface-raised/50",
        unread && "border-l-2 border-l-primary bg-primary/10",
        dimmed && "opacity-55",
      )}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span
          className={cn(
            "truncate font-mono text-xs font-semibold",
            roleTextClassOf(message.authorRole),
          )}
        >
          {authorLabel(message.authorAlias, message.authorRole)}
        </span>
        <time dateTime={message.createdAt} className="shrink-0 text-xs text-muted">
          {formatRelativeTime(message.createdAt)}
        </time>
      </div>
      <p className="mt-1 whitespace-pre-wrap break-words text-sm text-foreground">{message.text}</p>
      {message.mentions.length > 0 ? (
        <p className="mt-1 text-xs text-info">
          {message.mentions.map((alias) => `@${alias}`).join(" ")}
        </p>
      ) : null}
    </li>
  );
}
