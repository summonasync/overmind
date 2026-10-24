"use client";

import { cn } from "@/shared/lib/cn";
import { EmptyState } from "@/shared/ui/empty-state";
import { formatRelativeTime } from "@/shared/lib/relative-time";
import type { WaveMessage } from "@/features/wave-chat/types";

interface WaveMessageListProps {
  messages: WaveMessage[];
  ownerAlias: string;
}

export function WaveMessageList({ messages, ownerAlias }: WaveMessageListProps) {
  if (messages.length === 0) {
    return (
      <EmptyState
        title="No messages yet"
        hint="Post a signal below — lane agents pick it up on their next unread fetch."
      />
    );
  }

  return (
    <ol className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-3">
      {messages.map((message) => (
        <li
          key={message.id}
          className={cn(
            "rounded-md px-2.5 py-2",
            message.authorAlias === ownerAlias ? "bg-primary/5" : "bg-surface-raised/50",
          )}
        >
          <div className="flex items-baseline justify-between gap-2">
            <span className="truncate font-mono text-xs font-semibold text-primary">
              {message.authorAlias}
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
      ))}
    </ol>
  );
}
