"use client";

import { useEffect, useRef, useState } from "react";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import { cn } from "@/shared/lib/cn";
import { useAgentUnreadCount } from "@/features/agent-chat";
import { setBubbleOpen, toggleBubbleOpen, useBubbleOpen } from "@/widgets/agent-chat-bubble/lib/bubble-store";
import { usePopupDismiss } from "@/widgets/agent-chat-bubble/lib/use-popup-dismiss";
import { useBubblePose } from "@/widgets/agent-chat-bubble/lib/use-bubble-pose";
import { AgentChatPopup } from "@/widgets/agent-chat-bubble/ui/agent-chat-popup";

export function AgentChatBubble() {
  const open = useBubbleOpen();
  const pose = useBubblePose();
  const unreadCount = useAgentUnreadCount();
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const toggleRef = useRef<HTMLButtonElement | null>(null);
  const wasOpenRef = useRef(false);
  const [conversationId, setConversationId] = useState<string | null>(null);

  usePopupDismiss(open, surfaceRef, () => setBubbleOpen(false));

  useEffect(() => {
    if (wasOpenRef.current && !open) {
      toggleRef.current?.focus();
    }
    wasOpenRef.current = open;
  }, [open]);

  return (
    <div
      ref={surfaceRef}
      className="pointer-events-none fixed bottom-4 right-4 z-40 flex flex-col-reverse items-end gap-2 sm:bottom-6 sm:right-6"
    >
      <button
        ref={toggleRef}
        type="button"
        onClick={toggleBubbleOpen}
        aria-expanded={open}
        aria-label={unreadCount > 0 ? `Open agent chat, ${unreadCount} unread` : "Open agent chat"}
        title="Agent chat"
        className={cn(
          "pointer-events-auto relative rounded-full border bg-surface-raised p-2 shadow-lg transition-colors",
          open ? "border-primary/50" : "border-border hover:border-primary/40",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
        )}
      >
        <Zerling pose={pose} size={ZERLING_SIZES.banner} />
        {unreadCount > 0 ? (
          <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-danger px-1 text-[10px] font-bold leading-none text-background ring-2 ring-background">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        ) : null}
      </button>
      {open ? (
        <AgentChatPopup
          conversationId={conversationId}
          onConversationChange={setConversationId}
          onClose={() => setBubbleOpen(false)}
        />
      ) : null}
    </div>
  );
}
