import type { BadgeTone } from "@/shared/ui/badge";
import type { IssueKind } from "@/features/issues/types";

const KIND_TONES: Record<IssueKind, BadgeTone> = {
  issue: "neutral",
  request: "info",
};

export function issueKindTone(kind: IssueKind): BadgeTone {
  return KIND_TONES[kind];
}
