export const ISSUE_STATUSES = ["open", "triaged", "promoted", "closed"] as const;

export type IssueStatus = (typeof ISSUE_STATUSES)[number];

export interface IssueRow {
  id: string;
  title: string;
  status: IssueStatus;
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
  authorProjectId: string;
  createdAt: string;
  closedAt: string | null;
  promotedToArtifactId: string | null;
  appendCount: number;
}

export interface IssueListFilter {
  status: IssueStatus | null;
  authorProjectId: string | null;
}

export interface AppendIssueValues {
  authorProjectId: string;
  text: string;
}

export interface PromoteIssueValues {
  featureSlug: string;
}
