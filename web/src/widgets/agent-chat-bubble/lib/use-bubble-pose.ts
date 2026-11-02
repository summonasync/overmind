"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { agentUnreadCountQueryOptions, useChatActivity } from "@/features/agent-chat";
import { useBubbleOpen } from "@/widgets/agent-chat-bubble/lib/bubble-store";
import {
  resolveBubblePose,
  SMUG_HOLD_MS,
  type BubblePose,
} from "@/widgets/agent-chat-bubble/lib/bubble-pose";

export function useBubblePose(): BubblePose {
  const open = useBubbleOpen();
  const activity = useChatActivity();
  const unread = useQuery(agentUnreadCountQueryOptions());
  const [pose, setPose] = useState<BubblePose>("sleeping");

  useEffect(() => {
    const applyPose = () => {
      setPose(
        resolveBubblePose({
          open,
          unreadCount: unread.data ?? 0,
          activity,
          nowMs: Date.now(),
        }),
      );
    };
    applyPose();
    if (activity.lastTurn.kind !== "completed" || !activity.lastTurn.delegated) {
      return;
    }
    const remaining = activity.lastTurn.completedAtMs + SMUG_HOLD_MS - Date.now();
    const timer = window.setTimeout(applyPose, Math.max(remaining, 0));
    return () => window.clearTimeout(timer);
  }, [open, unread.data, activity]);

  return pose;
}
