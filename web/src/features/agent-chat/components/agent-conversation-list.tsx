"use client";

import { Trash2 } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { formatRelativeTime } from "@/shared/lib/relative-time";
import type { AgentConversation } from "@/features/agent-chat/types";

interface AgentConversationListProps {
  conversations: AgentConversation[];
  activeId: string | null;
  onOpen: (conversationId: string) => void;
  onDelete: (conversationId: string) => void;
}

export function AgentConversationList({
  conversations,
  activeId,
  onOpen,
  onDelete,
}: AgentConversationListProps) {
  if (conversations.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-surface px-3 py-6 text-center text-xs text-muted">
        No conversations yet — your first message opens one.
      </div>
    );
  }

  return (
    <nav aria-label="Conversations" className="flex flex-col gap-1">
      {conversations.map((conversation) => {
        const active = conversation.id === activeId;
        const title = conversation.title?.trim() || "Untitled";
        return (
          <div
            key={conversation.id}
            className={cn(
              "group flex items-center gap-1 rounded-md pr-1 transition-colors",
              active ? "bg-primary/10" : "hover:bg-surface-raised",
            )}
          >
            <button
              type="button"
              onClick={() => onOpen(conversation.id)}
              aria-current={active ? "true" : undefined}
              className="min-w-0 flex-1 rounded-md px-2.5 py-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              <span
                className={cn(
                  "block truncate text-sm font-medium",
                  active ? "text-primary" : "text-foreground",
                )}
              >
                {title}
              </span>
              <span className="block text-xs text-muted">
                {formatRelativeTime(conversation.updatedAt)}
              </span>
            </button>
            <button
              type="button"
              aria-label={`Delete conversation ${title}`}
              onClick={() => onDelete(conversation.id)}
              className="rounded p-1.5 text-muted opacity-0 transition-opacity hover:bg-danger/10 hover:text-danger focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 group-hover:opacity-100"
            >
              <Trash2 size={14} aria-hidden />
            </button>
          </div>
        );
      })}
    </nav>
  );
}
