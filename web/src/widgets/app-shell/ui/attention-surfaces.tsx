"use client";

import { useState } from "react";
import { isAwaitingNotification, useNotifications } from "@/features/notifications";
import { useAttentionSurfaces } from "@/widgets/app-shell/lib/use-attention-surfaces";
import { AttentionToast } from "./attention-toast";

// Newest first is the list's own order, so the head is what the owner sees.
const MAX_TOASTS = 3;

export function AttentionSurfaces() {
  useAttentionSurfaces();
  const { items } = useNotifications();
  const [dismissed, setDismissed] = useState<ReadonlySet<string>>(new Set());
  const cards = items
    .filter((item) => isAwaitingNotification(item) && !dismissed.has(item.id))
    .slice(0, MAX_TOASTS);
  const dismiss = (id: string) => {
    setDismissed((current) => new Set(current).add(id));
  };

  if (cards.length === 0) {
    return null;
  }

  return (
    <div
      aria-label="Attention toasts"
      className="pointer-events-none fixed bottom-24 right-4 z-40 flex flex-col items-end gap-2"
    >
      {cards.map((item) => (
        <AttentionToast key={item.id} item={item} onDismiss={dismiss} />
      ))}
    </div>
  );
}
