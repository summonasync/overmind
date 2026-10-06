import { awaitingTitle } from "./awaiting-title.ts";

const BASE_TITLE = "Overmind";

export function titleFor(
  projectName: string | null,
  awaitingCount: number,
  attentionCount = 0,
): string {
  const base = projectName === null ? BASE_TITLE : `${projectName} — ${BASE_TITLE}`;
  return awaitingTitle(base, Math.max(awaitingCount, attentionCount));
}
