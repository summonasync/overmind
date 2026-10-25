import { Badge } from "@/shared/ui/badge";
import { issueKindTone } from "@/features/issues/lib/kind-tone";
import type { IssueKind } from "@/features/issues/types";

interface IssueKindBadgeProps {
  kind: IssueKind;
}

export function IssueKindBadge({ kind }: IssueKindBadgeProps) {
  return <Badge tone={issueKindTone(kind)}>{kind}</Badge>;
}
