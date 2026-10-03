import type { IssueStatus } from "@/features/issues/types";

export interface IssueActionAvailability {
  canTriage: boolean;
  canClose: boolean;
  canPromote: boolean;
}

const TRIAGE_SOURCES: IssueStatus[] = ["open"];
const CLOSE_SOURCES: IssueStatus[] = ["open", "triaged", "promoted"];
const PROMOTE_SOURCES: IssueStatus[] = ["open", "triaged"];

export function issueActionAvailability(status: IssueStatus): IssueActionAvailability {
  return {
    canTriage: TRIAGE_SOURCES.includes(status),
    canClose: CLOSE_SOURCES.includes(status),
    canPromote: PROMOTE_SOURCES.includes(status),
  };
}
