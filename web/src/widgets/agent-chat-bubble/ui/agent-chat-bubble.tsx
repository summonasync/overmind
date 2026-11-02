"use client";

import { useRef, useState } from "react";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import { cn } from "@/shared/lib/cn";
import { setBubbleOpen, toggleBubbleOpen, useBubbleOpen } from "@/widgets/agent-chat-bubble/lib/bubble-store";
import { usePopupDismiss } from "@/widgets/agent-chat-bubble/lib/use-popup-dismiss";
import { AgentChatPopup } from "@/widgets/agent-chat-bubble/ui/agent-chat-popup";

export function AgentChatBubble() {
  const open = useBubbleOpen();
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const [conversationId, setConversationId] = useState<string | null>(null);

  usePopupDismiss(open, surfaceRef, () => setBubbleOpen(false));

  return (
    <div
      ref={surfaceRef}
      className="pointer-events-none fixed bottom-4 right-4 z-40 flex flex-col items-end gap-2 sm:bottom-6 sm:right-6"
    >
      {open ? (
        <AgentChatPopup
          conversationId={conversationId}
          onConversationChange={setConversationId}
          onClose={() => setBubbleOpen(false)}
        />
      ) : null}
      <button
        type="button"
        onClick={toggleBubbleOpen}
        aria-expanded={open}
        aria-label={open ? "Close agent chat" : "Open agent chat"}
        title="Agent chat"
        className={cn(
          "pointer-events-auto rounded-full border bg-surface-raised p-2 shadow-lg transition-colors",
          open ? "border-primary/50" : "border-border hover:border-primary/40",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background",
        )}
      >
        <Zerling pose="sleeping" size={ZERLING_SIZES.banner} />
      </button>
    </div>
  );
}
