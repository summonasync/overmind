import type { BadgeTone } from "@/shared/ui/badge";
import type { PlanningFeatureBoardItem } from "@/features/planning/types";

export interface TaskReadinessSegment {
  tone: BadgeTone;
  count: number;
  fraction: number;
}

export interface TaskReadiness {
  segments: TaskReadinessSegment[];
  countersLine: string;
  ariaValueText: string;
}

const READINESS_SEGMENTS = [
  { key: "todo", tone: "neutral", counter: "todo", aria: "todo" },
  { key: "inProgress", tone: "warning", counter: "in-progress", aria: "in progress" },
  { key: "review", tone: "info", counter: "review", aria: "in review" },
  { key: "done", tone: "success", counter: "done", aria: "done" },
  { key: "blocked", tone: "danger", counter: "blocked", aria: "blocked" },
] as const;

export function taskReadiness(item: PlanningFeatureBoardItem): TaskReadiness | null {
  if (item.taskTotal === 0) {
    return null;
  }
  if (item.taskCounts === undefined) {
    return doneRestReadiness(item.taskDone, item.taskTotal);
  }
  const counts = item.taskCounts;
  const total = item.taskTotal;
  return {
    segments: READINESS_SEGMENTS.map(({ key, tone }) => ({
      tone,
      count: counts[key],
      fraction: counts[key] / total,
    })),
    countersLine: READINESS_SEGMENTS.filter(({ key }) => key !== "blocked" || counts.blocked > 0)
      .map(({ key, counter }) => `${counter} ${counts[key]}|${total}`)
      .join(" · "),
    ariaValueText: READINESS_SEGMENTS.map(({ key, aria }) => `${counts[key]} ${aria}`).join(", "),
  };
}

function doneRestReadiness(done: number, total: number): TaskReadiness {
  const rest = total - done;
  return {
    segments: [
      { tone: "success", count: done, fraction: done / total },
      { tone: "neutral", count: rest, fraction: rest / total },
    ],
    countersLine: `done ${done}|${total} · rest ${rest}|${total}`,
    ariaValueText: `${done} done, ${rest} remaining`,
  };
}
