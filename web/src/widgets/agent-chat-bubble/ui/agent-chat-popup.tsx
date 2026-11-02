"use client";

import { useState } from "react";
import { ChevronDown, X } from "lucide-react";
import {
  AgentComposer,
  AgentConversationList,
  AgentMessageList,
  useAgentChat,
  useAgentConversationList,
  useConversationRestore,
} from "@/features/agent-chat";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { Zerling } from "@/shared/ui/zerling";
import { cn } from "@/shared/lib/cn";

interface AgentChatPopupProps {
  conversationId: string | null;
  onConversationChange: (conversationId: string | null) => void;
  onClose: () => void;
}

export function AgentChatPopup({ conversationId, onConversationChange, onClose }: AgentChatPopupProps) {
  const [listOpen, setListOpen] = useState(false);
  const list = useAgentConversationList({
    projectId: null,
    onRemoved: (removedId) => {
      if (conversationId === removedId) {
        onConversationChange(null);
      }
    },
  });
  useConversationRestore({
    conversations: list.conversations,
    isPending: list.isPending,
    selectedId: conversationId,
    select: onConversationChange,
  });
  const chat = useAgentChat({
    conversationId,
    projectId: null,
    onConversationCreated: (conversation) => onConversationChange(conversation.id),
  });

  const activeConversation = list.conversations.find((conversation) => conversation.id === conversationId);
  const activeTitle = activeConversation?.title?.trim() || null;
  const surfaceError = list.error ?? chat.historyError;

  const openConversation = (conversationId: string) => {
    list.clearError();
    onConversationChange(conversationId);
    setListOpen(false);
  };

  return (
    <div
      role="dialog"
      aria-label="Agent chat"
      className="pointer-events-auto flex max-h-[min(70dvh,32rem)] w-[min(380px,calc(100vw-2rem))] animate-bubble-pop flex-col overflow-hidden rounded-xl border border-border-strong bg-surface-raised shadow-xl sm:h-[520px] sm:max-h-[calc(100dvh-10rem)] sm:w-[380px]"
    >
      <header className="flex shrink-0 items-center gap-2 border-b border-border px-3 py-2">
        <Zerling pose="sleeping" size={24} />
        <span className="text-sm font-semibold text-foreground">Agent chat</span>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close agent chat"
          className="ml-auto rounded p-1 text-muted transition-colors hover:bg-surface hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <X size={14} aria-hidden />
        </button>
      </header>
      <div className="shrink-0 border-b border-border">
        <button
          type="button"
          onClick={() => setListOpen((current) => !current)}
          aria-expanded={listOpen}
          className="flex w-full items-center justify-between gap-2 px-3 py-1.5 text-xs text-muted transition-colors hover:bg-surface hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/40"
        >
          <span className="truncate">{activeTitle ?? "Conversations"}</span>
          <ChevronDown
            size={14}
            aria-hidden
            className={cn("shrink-0 transition-transform", listOpen && "rotate-180")}
          />
        </button>
        {listOpen ? (
          <div className="max-h-40 overflow-y-auto px-2 pb-2">
            {list.isPending ? (
              <LoadingState label="Loading conversations…" />
            ) : (
              <AgentConversationList
                conversations={list.conversations}
                activeId={conversationId}
                onOpen={openConversation}
                onDelete={list.remove}
              />
            )}
          </div>
        ) : null}
      </div>
      {surfaceError ? (
        <div className="shrink-0 px-2 pt-2">
          <ErrorText error={surfaceError} />
        </div>
      ) : null}
      <section aria-label="Conversation" className="flex min-h-0 flex-1 flex-col px-2 pt-2">
        <AgentMessageList
          conversationId={conversationId}
          messages={chat.messages}
          streaming={chat.streaming}
          historyLoading={conversationId !== null && chat.historyLoading}
          canLoadOlder={chat.canLoadOlder}
          loadingOlder={chat.loadingOlder}
          onLoadOlder={chat.loadOlder}
        />
      </section>
      <div className="shrink-0 p-2">
        <AgentComposer
          streaming={chat.streaming}
          error={chat.sendError}
          onSend={chat.send}
          onStop={chat.stop}
        />
      </div>
    </div>
  );
}
