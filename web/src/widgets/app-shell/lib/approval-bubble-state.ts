import type { NotificationNudgeMode } from "@/features/settings";

export type BubbleSurface = "bubble" | "icon" | "hidden";

export const APPROVAL_NAG_TTL_MS = 14 * 24 * 60 * 60_000;

const MINUTE_MS = 60_000;

export interface BubbleCandidate {
  id: string;
  createdAtMs: number;
}

export interface BubbleDiscipline {
  seenIds: string[];
  nagNextPopMs: number | null;
  miniCollapsed: boolean;
}

export const initialBubbleDiscipline: BubbleDiscipline = {
  seenIds: [],
  nagNextPopMs: null,
  miniCollapsed: false,
};

export function bubbleCandidates(items: BubbleCandidate[], nowMs: number): BubbleCandidate[] {
  return items
    .filter((item) => nowMs - item.createdAtMs < APPROVAL_NAG_TTL_MS)
    .sort((a, b) => a.createdAtMs - b.createdAtMs);
}

export function bubbleMoreCount(candidates: BubbleCandidate[]): number {
  return Math.max(0, candidates.length - 1);
}

export function bubbleSurface(
  discipline: BubbleDiscipline,
  mode: NotificationNudgeMode,
  candidates: BubbleCandidate[],
  nowMs: number,
): BubbleSurface {
  if (candidates.length === 0) {
    return "hidden";
  }
  if (mode === "mini") {
    return discipline.miniCollapsed ? "icon" : "bubble";
  }
  if (mode === "nag") {
    return discipline.nagNextPopMs !== null && nowMs < discipline.nagNextPopMs ? "hidden" : "bubble";
  }
  return candidates.some((candidate) => !discipline.seenIds.includes(candidate.id)) ? "bubble" : "hidden";
}

export function dismissBubble(
  candidateIds: string[],
  nowMs: number,
  intervalMinutes: number,
): BubbleDiscipline {
  return {
    seenIds: candidateIds,
    nagNextPopMs: nowMs + intervalMinutes * MINUTE_MS,
    miniCollapsed: true,
  };
}

export function expandBubble(discipline: BubbleDiscipline): BubbleDiscipline {
  return { ...discipline, miniCollapsed: false };
}
