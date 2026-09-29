"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { boardQueryOptions } from "@/features/planning";
import { isAwaitingResponse, useNotifications } from "@/features/notifications";
import type { NotificationItem } from "@/features/notifications";
import { useSettings } from "@/features/settings";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";
import { parseInstant } from "@/shared/lib/relative-time";
import { readJson, writeJson } from "@/shared/lib/session-json";
import {
  bubbleCandidates,
  bubbleMoreCount,
  bubbleSurface,
  dismissBubble,
  expandBubble,
  initialBubbleDiscipline,
} from "@/widgets/app-shell/lib/approval-bubble-state";
import type { NotificationKindFilter } from "@/widgets/app-shell/lib/notification-kind-filter";
import type {
  BubbleCandidate,
  BubbleDiscipline,
} from "@/widgets/app-shell/lib/approval-bubble-state";

const DISCIPLINE_STORAGE_KEY = "overmind.approval-bubble.discipline";
const DEFAULT_NAG_INTERVAL_MINUTES = 10;

function storedDiscipline(): BubbleDiscipline {
  return readJson(DISCIPLINE_STORAGE_KEY, initialBubbleDiscipline);
}

function toCandidate(item: NotificationItem): BubbleCandidate | null {
  const createdAtMs = parseInstant(item.createdAt);
  return createdAtMs === null ? null : { id: item.id, createdAtMs };
}

export function useApprovalBubble() {
  const { items } = useNotifications();
  const { data: settings } = useSettings();
  const [nowMs, setNowMs] = useState(() => Date.now());
  const [discipline, setDiscipline] = useState<BubbleDiscipline>(storedDiscipline);

  useEffect(() => {
    const timer = window.setInterval(() => setNowMs(Date.now()), LIVE_SYNC_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, []);

  const awaitingItems = useMemo(() => items.filter(isAwaitingResponse), [items]);

  const candidates = useMemo(
    () =>
      bubbleCandidates(
        awaitingItems
          .map(toCandidate)
          .filter((candidate): candidate is BubbleCandidate => candidate !== null),
        nowMs,
      ),
    [awaitingItems, nowMs],
  );

  const item = items.find((entry) => entry.id === candidates[0]?.id) ?? null;
  const mode = settings?.notificationNudgeMode ?? "quiet";
  const intervalMinutes = settings?.notificationNudgeIntervalMinutes ?? DEFAULT_NAG_INTERVAL_MINUTES;
  const surface = bubbleSurface(discipline, mode, candidates, nowMs);
  const moreCount = bubbleMoreCount(candidates);

  const board = useQuery({
    ...boardQueryOptions(item?.projectId ?? ""),
    enabled: surface !== "hidden" && (item?.projectId ?? "").length > 0,
  });
  const featureSlug = item?.featureSlug ?? null;
  const deadTarget =
    featureSlug !== null &&
    board.data !== undefined &&
    !board.data.some((feature) => feature.slug === featureSlug);

  const sync = useCallback((next: BubbleDiscipline) => {
    setDiscipline(next);
    writeJson(DISCIPLINE_STORAGE_KEY, next);
  }, []);

  const dismiss = useCallback(() => {
    sync(dismissBubble(candidates.map((candidate) => candidate.id), Date.now(), intervalMinutes));
  }, [candidates, intervalMinutes, sync]);

  const expand = useCallback(() => {
    sync(expandBubble(discipline));
  }, [discipline, sync]);

  const centerKind: NotificationKindFilter = awaitingItems.some(
    (entry) => entry.kind === "question",
  )
    ? "all"
    : "approval";

  return {
    surface,
    item,
    moreCount,
    deadTarget,
    dismiss,
    expand,
    awaitingCount: awaitingItems.length,
    centerKind,
  };
}
