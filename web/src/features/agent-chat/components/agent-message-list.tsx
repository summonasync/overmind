"use client";

import { useEffect, useRef } from "react";
import { Button } from "@/shared/ui/button";
import { cn } from "@/shared/lib/cn";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { formatRelativeTime } from "@/shared/lib/relative-time";
import { AGENT_SCROLL_BOTTOM_THRESHOLD } from "@/features/agent-chat/lib/limits";
import { useSeenMessageTracking } from "@/features/agent-chat/lib/use-seen-tracking";
import { AgentRouteTraceRow } from "@/features/agent-chat/components/agent-route-trace";
import type { AgentMessage, AgentRouteTrace } from "@/features/agent-chat/types";

interface AgentMessageListProps {
  conversationId: string | null;
  messages: AgentMessage[];
  routeTraces: AgentRouteTrace[];
  streaming: boolean;
  historyLoading: boolean;
  canLoadOlder: boolean;
  loadingOlder: boolean;
  onLoadOlder: () => Promise<void>;
}

export function AgentMessageList({
  conversationId,
  messages,
  routeTraces,
  streaming,
  historyLoading,
  canLoadOlder,
  loadingOlder,
  onLoadOlder,
}: AgentMessageListProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const nearBottomRef = useRef(true);
  const scrollKey = conversationId ?? "new";

  useSeenMessageTracking(conversationId, messages, containerRef);

  useEffect(() => {
    nearBottomRef.current = true;
    const frame = requestAnimationFrame(() => {
      const container = containerRef.current;
      if (container !== null) {
        container.scrollTop = container.scrollHeight;
      }
    });
    return () => cancelAnimationFrame(frame);
  }, [scrollKey]);

  useEffect(() => {
    if (!nearBottomRef.current) {
      return;
    }
    const container = containerRef.current;
    if (container !== null) {
      container.scrollTop = container.scrollHeight;
    }
  }, [messages, routeTraces, streaming]);

  const handleScroll = () => {
    const container = containerRef.current;
    if (container === null) {
      return;
    }
    const distance = container.scrollHeight - container.scrollTop - container.clientHeight;
    nearBottomRef.current = distance < AGENT_SCROLL_BOTTOM_THRESHOLD;
  };

  const pageKeepingViewport = (load: () => Promise<void>) => async () => {
    const container = containerRef.current;
    const previousTop = container?.scrollTop ?? 0;
    const previousHeight = container?.scrollHeight ?? 0;
    await load();
    if (container !== null && !nearBottomRef.current) {
      container.scrollTop = container.scrollHeight - previousHeight + previousTop;
    }
  };

  if (historyLoading) {
    return <LoadingState label="Loading messages…" />;
  }

  if (messages.length === 0) {
    return (
      <EmptyState
        title="No messages yet"
        hint="Send the first message below — the conversation and the agent's reply are stored here."
      />
    );
  }

  return (
    <div className="flex min-h-0 flex-col rounded-lg border border-border bg-surface">
      {canLoadOlder ? (
        <div className="flex items-center justify-center border-b border-border px-3 py-2">
          <Button
            variant="ghost"
            className="text-xs"
            disabled={loadingOlder}
            onClick={pageKeepingViewport(onLoadOlder)}
          >
            {loadingOlder ? "Loading…" : "Load earlier messages"}
          </Button>
        </div>
      ) : null}
      <div
        ref={containerRef}
        onScroll={handleScroll}
        className="max-h-[60vh] min-h-0 overflow-y-auto"
      >
        <ol className="flex flex-col gap-2 p-3" aria-live="polite">
          {messages.map((message) => (
            <MessageRow key={message.id} message={message} />
          ))}
          <AgentRouteTraceRow traces={routeTraces} />
          {streaming ? (
            <li className="px-2.5 py-1 text-xs text-muted">
              <span className="animate-pulse">The agent is replying…</span>
            </li>
          ) : null}
        </ol>
      </div>
    </div>
  );
}

interface MessageRowProps {
  message: AgentMessage;
}

function MessageRow({ message }: MessageRowProps) {
  const author = message.role === "user" ? "You" : "Agent";
  return (
    <li
      data-agent-message-id={message.id}
      className={cn(
        "rounded-md px-2.5 py-2",
        message.role === "user" ? "bg-primary/5" : "bg-surface-raised/50",
      )}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span
          className={cn(
            "font-mono text-xs font-semibold",
            message.role === "user" ? "text-primary" : "text-agent",
          )}
        >
          {author}
        </span>
        <time dateTime={message.createdAt} className="shrink-0 text-xs text-muted">
          {formatRelativeTime(message.createdAt)}
        </time>
      </div>
      <p className="mt-1 whitespace-pre-wrap break-words text-sm text-foreground">{message.content}</p>
    </li>
  );
}
