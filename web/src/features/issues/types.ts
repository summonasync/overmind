export const ISSUE_STATUSES = ["open", "triaged", "promoted", "closed"] as const;

export type IssueStatus = (typeof ISSUE_STATUSES)[number];

export const ISSUE_KINDS = ["issue", "request"] as const;

export type IssueKind = (typeof ISSUE_KINDS)[number];

export interface IssueRow {
  id: string;
  title: string;
  status: IssueStatus;
  kind: IssueKind;
  authorProjectId: string;
  createdAt: string;
  closedAt: string | null;
  promotedToArtifactId: string | null;
  appendCount: number;
}

export interface IssueAppendItem {
  id: string;
  authorProjectId: string;
  text: string;
  createdAt: string;
}

export interface IssueCard extends IssueRow {
  body: string;
  appends: IssueAppendItem[];
}

export interface CrossProjectIssueRow {
  id: string;
  projectId: string;
  title: string;
  status: IssueStatus;
  kind: IssueKind;
  authorProjectId: string;
  createdAt: string;
  closedAt: string | null;
  promotedToArtifactId: string | null;
  appendCount: number;
}

export interface IssueListFilter {
  status: IssueStatus | null;
  kind: IssueKind | null;
  authorProjectId: string | null;
}

export interface AppendIssueValues {
  authorProjectId: string;
  text: string;
}

export interface PromoteIssueValues {
  featureSlug: string;
}
