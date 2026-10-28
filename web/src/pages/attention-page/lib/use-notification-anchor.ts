"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  ANCHOR_POLL_INTERVAL_MS,
  ANCHOR_POLL_TIMEOUT_MS,
  NOTIFICATION_HIGHLIGHT_MS,
  NOTIFICATION_PARAM,
  notificationAnchorSelector,
} from "@/features/notifications";

export function useNotificationAnchor(): string | null {
  const searchParams = useSearchParams();
  const notificationId = searchParams?.get(NOTIFICATION_PARAM) ?? null;
  const [highlightedId, setHighlightedId] = useState<string | null>(null);

  useEffect(() => {
    if (notificationId === null) {
      return;
    }
    const selector = notificationAnchorSelector(notificationId);
    const startedAt = Date.now();
    const poll = window.setInterval(() => {
      const anchor = document.querySelector(selector);
      if (anchor === null) {
        if (Date.now() - startedAt >= ANCHOR_POLL_TIMEOUT_MS) {
          window.clearInterval(poll);
        }
        return;
      }
      window.clearInterval(poll);
      anchor.scrollIntoView({ behavior: "smooth", block: "center" });
      setHighlightedId(notificationId);
      window.setTimeout(() => {
        setHighlightedId((current) => (current === notificationId ? null : current));
      }, NOTIFICATION_HIGHLIGHT_MS);
    }, ANCHOR_POLL_INTERVAL_MS);
    return () => window.clearInterval(poll);
  }, [notificationId]);

  return highlightedId;
}
