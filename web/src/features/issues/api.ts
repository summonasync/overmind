import { apiGet, apiPost } from "@/shared/api/client";
import type {
  AppendIssueValues,
  CrossProjectIssueRow,
  IssueCard,
  IssueKind,
  IssueListFilter,
  IssueRow,
  IssueStatus,
  PromoteIssueValues,
} from "@/features/issues/types";

function listPath(projectId: string, filter: IssueListFilter): string {
  const params = new URLSearchParams();
  if (filter.status !== null) {
    params.set("status", filter.status);
  }
  if (filter.kind !== null) {
    params.set("kind", filter.kind);
  }
  if (filter.authorProjectId !== null && filter.authorProjectId.length > 0) {
    params.set("authorProjectId", filter.authorProjectId);
  }
  const query = params.toString();
  return `/projects/${projectId}/issues${query.length > 0 ? `?${query}` : ""}`;
}

export function listIssues(projectId: string, filter: IssueListFilter): Promise<IssueRow[]> {
  return apiGet<IssueRow[]>(listPath(projectId, filter));
}

export function listAllIssues(kind: IssueKind | null): Promise<CrossProjectIssueRow[]> {
  const query = kind !== null ? `?kind=${kind}` : "";
  return apiGet<CrossProjectIssueRow[]>(`/issues${query}`);
}

export function getIssueCard(projectId: string, issueId: string): Promise<IssueCard> {
  return apiGet<IssueCard>(`/projects/${projectId}/issues/${issueId}`);
}

export function appendToIssue(
  projectId: string,
  issueId: string,
  values: AppendIssueValues,
): Promise<void> {
  return apiPost<void>(`/projects/${projectId}/issues/${issueId}/append`, {
    authorProjectId: values.authorProjectId,
    text: values.text,
  });
}

export function setIssueStatus(
  projectId: string,
  issueId: string,
  status: IssueStatus,
): Promise<void> {
  return apiPost<void>(`/projects/${projectId}/issues/${issueId}/status`, {
    status,
    source: "ui",
  });
}

export function promoteIssue(
  projectId: string,
  issueId: string,
  values: PromoteIssueValues,
): Promise<void> {
  return apiPost<void>(`/projects/${projectId}/issues/${issueId}/promote`, {
    featureSlug: values.featureSlug,
    source: "ui",
  });
}
